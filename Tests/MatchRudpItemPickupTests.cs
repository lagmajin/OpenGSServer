using System;
using System.Threading;
using LiteNetLib;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Drives a field item pickup over the realtime transport, end to end.
/// <para>
/// The pickup route exists on the reliable channel and on the realtime one, and
/// the realtime one is what a real client uses during a match. Nothing exercised
/// it: the probe tests stopped at the connection and the damage smoke drove the
/// lobby stream, so the two places the realtime path could be broken, the
/// message type allow list and the dispatch name, were both wrong for a while
/// with nothing to notice.
/// </para>
/// <para>
/// The assertion is on the state the server ends up holding, not on a message
/// the client saw, because the server is what decides.
/// </para>
/// </summary>
// These tests each bind a real udp port, so two of them running at once
// can land on the same one. Sharing a collection keeps them on one lane.
[Collection("RealtimeSocket")]
public sealed class MatchRudpItemPickupTests : IDisposable
{
    private static int nextPort = 65250;

    private MatchUDPServer server = null!;
    private MatchRudpProbe probe = null!;
    private MatchRoom room = null!;
    private string playerId = string.Empty;
    private string roomId = string.Empty;

    public void Dispose()
    {
        probe?.Dispose();
        server?.Dispose();
    }

    /// <summary>
    /// Pumps both sides. The probe has to be polled for its own sends to go out
    /// and the server for them to arrive, so pumping only one leaves the other
    /// waiting forever.
    /// </summary>
    private void Pump(int milliseconds)
    {
        probe?.Poll(milliseconds, () =>
        {
            server.PollingEvent();
            server.Tick(0.04f);
        });
    }

    private void SetUpRoom()
    {
        playerId = "picker-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        // The realtime parser resolves the room through MatchRoomManager.Instance,
        // so a standalone manager would build a room nothing can find.
        var rooms = MatchRoomManager.Instance;
        var created = rooms.CreateNewDeathMatchRoom("rudp-item", playerId, capacity: 4);
        roomId = created.RoomId;
        room = rooms.GetRoomById(created.RoomId) as MatchRoom;
        Assert.NotNull(room);
        room!.AddNewPlayer(new PlayerInfo(playerId, "Picker"));

        var lag = MatchServerV2.Instance.ServerLagCompensationManager;
        lag.StartMatch(roomId);
        lag.AddPlayer(playerId);

        server = new MatchUDPServer();
        server.Listen(Interlocked.Increment(ref nextPort));
        Pump(200);

        var token = server.IssueConnectionToken(playerId);
        Assert.False(string.IsNullOrWhiteSpace(token), "no realtime token was issued");

        probe = new MatchRudpProbe();
        probe.Listener.NetworkReceiveEvent += (peer, reader, method) =>
        {
            var payload = reader.GetString();
            reader.Recycle();
            try
            {
                probe.Offer(JObject.Parse(payload));
            }
            catch (Exception)
            {
                // Test only; malformed payloads are ignored.
            }
        };

        Assert.True(
            probe.Connect("127.0.0.1", server.UdpPort ?? 0, playerId, token, roomId, 5000,
                () => { server.PollingEvent(); server.Tick(0.04f); }),
            "the realtime client did not complete the handshake");
    }

    /// <summary>
    /// Sends a pickup claim the way a client does, on the realtime channel.
    /// </summary>
    private void ClaimPickup(string itemId)
    {
        probe.Send(playerId, roomId, new JObject
        {
            ["MessageType"] = "ItemPickup",
            ["PlayerId"] = playerId,
            ["TargetID"] = playerId,
            ["ItemId"] = itemId
        });
    }

    /// <summary>
    /// Moves the player onto a spot and lets the server settle it.
    /// <para>
    /// It walks rather than teleports. The server only adopts a position within a
    /// tolerance of the one it holds, and a three unit jump at a 0.04s delta is
    /// outside that while a one unit step is inside it. A single packet claiming
    /// the far side of the room would simply be refused, and the player would
    /// never be where the test believes it is.
    /// </para>
    /// </summary>
    private void MoveTo(float x, float y)
    {
        const float stepSize = 1f;
        byte sequence = 1;

        float currentX = 0f;
        float currentY = 0f;
        while (MathF.Abs(currentX - x) > 0.001f || MathF.Abs(currentY - y) > 0.001f)
        {
            var dx = x - currentX;
            var dy = y - currentY;
            var distance = MathF.Sqrt((dx * dx) + (dy * dy));
            if (distance > stepSize)
            {
                dx = (dx / distance) * stepSize;
                dy = (dy / distance) * stepSize;
            }

            currentX += dx;
            currentY += dy;
            probe.SendPosition(playerId, roomId, currentX, currentY, 0f, 0f, 0.04f, sequence++);
            Pump(60);
        }

        // Settle at the destination, so the last packet the server accepted is the
        // spot the test is asserting about rather than one step short of it.
        for (var i = 0; i < 3; i++)
        {
            probe.SendPosition(playerId, roomId, currentX, currentY, 0f, 0f, 0.04f, sequence++);
            Pump(60);
        }
    }

    private string ItemState()
    {
        var json = MatchRoomManager.Instance.GetFieldItemManager(roomId)!.ToJson();
        return json.Count > 0 ? json[0]!["State"]?.ToString() ?? "unknown" : "no-item";
    }

    [Fact]
    public void AClaimOverRealtimeIsRoutedToTheItemManager()
    {
        SetUpRoom();
        var items = MatchRoomManager.Instance.GetFieldItemManager(roomId)!;
        var itemId = items.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        MoveTo(0f, 0f);
        ClaimPickup(itemId);
        Pump(400);

        // This is the assertion the missing allow list entry broke: the claim
        // never reached the dispatcher, so the item stayed on the ground.
        Assert.Equal("PickedUp", ItemState());
    }

    [Fact]
    public void AClaimFromAcrossTheMapIsRefused()
    {
        SetUpRoom();
        var items = MatchRoomManager.Instance.GetFieldItemManager(roomId)!;
        var itemId = items.SpawnItem(EFieldItemType.HealItem, 500f, 0f, 0f);

        MoveTo(0f, 0f);
        ClaimPickup(itemId);
        Pump(400);

        // The item exists and the id is right, but the player is nowhere near
        // the spawn, so the radius has to refuse it.
        Assert.Equal("Spawned", ItemState());
    }

    [Fact]
    public void AClaimWithoutAnAuthoritativePositionIsRefused()
    {
        SetUpRoom();
        var items = MatchRoomManager.Instance.GetFieldItemManager(roomId)!;
        var itemId = items.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        // No position reported, so the radius cannot be enforced and the item
        // id alone must not be enough.
        ClaimPickup(itemId);
        Pump(400);

        Assert.Equal("Spawned", ItemState());
    }

    [Fact]
    public void AClaimForAnUnknownItemIsInert()
    {
        SetUpRoom();
        MoveTo(0f, 0f);

        ClaimPickup("no-such-item");
        Pump(400);

        Assert.Equal("no-item", ItemState());
    }

    [Fact]
    public void AnItemCanOnlyBeClaimedOnceOverRealtime()
    {
        SetUpRoom();
        var items = MatchRoomManager.Instance.GetFieldItemManager(roomId)!;
        var itemId = items.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        MoveTo(0f, 0f);
        ClaimPickup(itemId);
        Pump(300);
        ClaimPickup(itemId);
        Pump(300);

        // A second claim for an item already taken changes nothing, so the
        // granted path cannot be triggered twice by a repeated message.
        Assert.Equal("PickedUp", ItemState());
        Assert.Equal(0, items.GetActiveItemCount());
    }

    // ---- Weapon drop -----------------------------------------------------

    /// <summary>
    /// Drops a weapon the way a client does, over the realtime channel.
    /// </summary>
    private void DropWeapon(string weaponType = "Rifle", int? magazine = null)
    {
        var payload = new JObject
        {
            ["MessageType"] = "WeaponDrop",
            ["WeaponType"] = weaponType
        };
        if (magazine.HasValue)
        {
            payload["MagazineAmmo"] = magazine.Value;
        }

        probe.Send(playerId, roomId, payload);
    }

    private JArray Items()
    {
        return MatchRoomManager.Instance.GetFieldItemManager(roomId)!.ToJson();
    }

    private JObject? WeaponItem()
    {
        foreach (var token in Items())
        {
            if (token["ItemType"]?.ToString() == nameof(EFieldItemType.WeaponItem))
            {
                return token as JObject;
            }
        }

        return null;
    }

    [Fact]
    public void AWeaponDroppedOverRealtimeAppearsAsAnItem()
    {
        SetUpRoom();
        MoveTo(3f, 0f);

        DropWeapon("Rifle", magazine: 17);
        Pump(400);

        // The drop message has to reach the item manager. The weapon used to be
        // instantiated by the dropping client alone, so it existed on one screen
        // and nowhere else: the server had no record of it and no other player
        // could ever see or pick it up.
        var weapon = WeaponItem();
        Assert.NotNull(weapon);
        Assert.Equal("Spawned", weapon!["State"]?.ToString());
        Assert.Equal("Rifle", weapon["WeaponType"]?.ToString());
        Assert.Equal(17, weapon["MagazineAmmo"]?.Value<int>());
    }

    [Fact]
    public void AWeaponIsDroppedWhereTheServerSaysThePlayerIs()
    {
        SetUpRoom();
        MoveTo(3f, 0f);

        // The message says the weapon is dropped at the origin, which is where
        // a client with something to gain from it would like it to appear.
        probe.Send(playerId, roomId, new JObject
        {
            ["MessageType"] = "WeaponDrop",
            ["WeaponType"] = "Rifle",
            ["PosX"] = 0f,
            ["PosY"] = 0f
        });
        Pump(400);

        var weapon = WeaponItem();
        Assert.NotNull(weapon);

        // The drop is placed from the position the server is holding, which is
        // the same reason a shot is resolved from it rather than from the message.
        Assert.Equal(3f, weapon!["PositionX"]?.Value<float>() ?? 0f, 2);
        Assert.Equal(0f, weapon["PositionY"]?.Value<float>() ?? 0f, 2);
    }

    [Fact]
    public void ADroppedWeaponKeepsItsRoundsThroughAStateRoundTrip()
    {
        SetUpRoom();
        MoveTo(3f, 0f);

        DropWeapon("Sniper", magazine: 3);
        Pump(400);

        // The magazine travels with the weapon, so it has to survive a state
        // sync. A dropped weapon that came back empty would silently lose the
        // rounds the player was carrying.
        var items = MatchRoomManager.Instance.GetFieldItemManager(roomId)!;
        var round = items.ToJson();
        items.LoadFromJson(round);

        Assert.Equal(3, WeaponItem()?["MagazineAmmo"]?.Value<int>());
    }

    [Fact]
    public void AWeaponDroppedWhereThePlayerStandsCanBePickedUpAgain()
    {
        SetUpRoom();
        MoveTo(3f, 0f);

        DropWeapon("Rifle", magazine: 12);
        Pump(400);

        var weapon = WeaponItem();
        Assert.NotNull(weapon);
        var itemId = weapon!["ItemId"]?.ToString() ?? "";

        // The drop goes through the same claim path as any other item, so the
        // pickup radius that was added for the spawn items governs a dropped
        // weapon too, and it can be taken back.
        ClaimPickup(itemId);
        Pump(400);

        // The claim is recorded on the item rather than removing it, so what
        // proves it was taken is the state, not its absence.
        Assert.True(
            MatchRoomManager.Instance.GetFieldItemManager(roomId)!.TryGetItem(itemId, out var taken),
            "the claimed weapon was not recorded at all");
        Assert.Equal("PickedUp", taken!.State);
        Assert.Equal(playerId, taken.PickedUpByPlayerId);
    }

    [Fact]
    public void ADropFromAPlayerWithNoPositionIsRefused()
    {
        SetUpRoom();

        // Nowhere to put it. A registered player with no position reads as the
        // origin, so this is the case where a drop would land in the middle of
        // the map for a player the server has never actually seen.
        DropWeapon("Rifle", magazine: 5);
        Pump(400);

        Assert.Null(WeaponItem());
    }

    [Fact]
    public void ADropWithNoWeaponTypeIsInert()
    {
        SetUpRoom();
        MoveTo(3f, 0f);

        DropWeapon(weaponType: "");
        Pump(400);

        Assert.Null(WeaponItem());
    }

    [Fact]
    public void AWeaponIsNotATimedBuff()
    {
        // Picking a weapon up is not a thirty second effect, and the paths that
        // fire for every pickup have to be able to tell the difference.
        Assert.False(FieldItemTypeNames.IsTimedBuff(EFieldItemType.WeaponItem));
        Assert.True(FieldItemTypeNames.IsCarriedEquipment(EFieldItemType.WeaponItem));
    }
}
