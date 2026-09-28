using System;
using System.Collections.Generic;
using System.Linq;
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

    // ---- Instant item use -----------------------------------------------

    /// <summary>
    /// Uses an instant item the way a client does, over the realtime channel.
    /// <para>
    /// The effect name is included because the client sends one. It is there to
    /// be ignored, and the test that passes a generous amount is the one that
    /// proves it is.
    /// </para>
    /// </summary>
    private void UseItem(string itemType, string effect = "heal", int? amount = null)
    {
        var payload = new JObject
        {
            ["MessageType"] = "ItemUseRequest",
            ["PlayerId"] = playerId,
            ["ItemType"] = itemType,
            ["Effect"] = effect
        };
        if (amount.HasValue)
        {
            payload["Amount"] = amount.Value;
        }

        probe.Send(playerId, roomId, payload);
    }

    private void Carry(EInstantItemType type)
    {
        Assert.True(room.TryGetPlayer(playerId, out var player), "the player left the room");
        player!.EquipInstantItems.Add(type);
    }

    private void SetHealth(int health)
    {
        Assert.True(room.TryGetPlayer(playerId, out var player), "the player left the room");
        player!.Health = health;
    }

    private int PlayerHealth()
    {
        Assert.True(room.TryGetPlayer(playerId, out var player), "the player left the room");
        return player!.Health;
    }

    private bool StillCarrying(EInstantItemType type)
    {
        Assert.True(room.TryGetPlayer(playerId, out var player), "the player left the room");
        return player!.EquipInstantItems.Contains(type);
    }

    [Fact]
    public void UsingAHealthKitOverRealtimeRaisesTheHealthTheServerHolds()
    {
        SetUpRoom();
        MoveTo(0f, 0f);

        SetHealth(40);
        Carry(EInstantItemType.HealthKit);

        // The message was dropped before the dispatcher ever saw it, so a spent
        // item did nothing on the server at all and only the spender's own screen
        // changed. The health the server holds is what is asserted here.
        UseItem("HealthKit");
        Pump(400);

        Assert.Equal(70, PlayerHealth());
    }

    [Fact]
    public void AHealthKitIsSpentWhenItIsUsed()
    {
        SetUpRoom();
        MoveTo(0f, 0f);

        SetHealth(40);
        Carry(EInstantItemType.HealthKit);

        UseItem("HealthKit");
        Pump(400);

        // Using an item and asking for one are different. Without the spend, a
        // client could use the same kit for the rest of the match.
        Assert.False(StillCarrying(EInstantItemType.HealthKit));
    }

    [Fact]
    public void TheAmountTheMessageClaimsIsNotWhatHeals()
    {
        SetUpRoom();
        MoveTo(0f, 0f);

        SetHealth(10);
        Carry(EInstantItemType.HealthKit);

        // The client used to name the effect and apply it to itself, so how much
        // a kit healed was whatever the sender said. A claim for a full heal from
        // ten health would have been granted.
        UseItem("HealthKit", effect: "heal", amount: 9999);
        Pump(400);

        Assert.Equal(40, PlayerHealth());
    }

    [Fact]
    public void AHealthKitCannotTakeHealthPastTheMaximum()
    {
        SetUpRoom();
        MoveTo(0f, 0f);

        SetHealth(90);
        Carry(EInstantItemType.HealthKit);

        UseItem("HealthKit");
        Pump(400);

        Assert.True(room.TryGetPlayer(playerId, out var player));
        Assert.Equal(player!.MaxHealth, PlayerHealth());
    }

    [Fact]
    public void AHealthKitIsSpentEvenAtFullHealth()
    {
        SetUpRoom();
        MoveTo(0f, 0f);

        Carry(EInstantItemType.HealthKit);
        var before = PlayerHealth();

        UseItem("HealthKit");
        Pump(400);

        // It did nothing, and it is still gone. A kit held in reserve is a kit the
        // player can use when they are hurt.
        Assert.Equal(before, PlayerHealth());
        Assert.False(StillCarrying(EInstantItemType.HealthKit));
    }

    [Fact]
    public void UsingAnItemThePlayerIsNotCarryingIsRefused()
    {
        SetUpRoom();
        MoveTo(0f, 0f);

        SetHealth(10);

        // Nothing carried. A client could otherwise spend an item it never picked
        // up, and the message is the only thing claiming it has one.
        UseItem("HealthKit");
        Pump(400);

        Assert.Equal(10, PlayerHealth());
        Assert.False(StillCarrying(EInstantItemType.HealthKit));
    }

    [Fact]
    public void TheSameItemCannotBeSpentTwice()
    {
        SetUpRoom();
        MoveTo(0f, 0f);

        SetHealth(10);
        Carry(EInstantItemType.HealthKit);

        UseItem("HealthKit");
        Pump(300);
        UseItem("HealthKit");
        Pump(300);

        // One kit, one heal. The second message arrives with nothing left to
        // spend, so it is refused rather than healing again.
        Assert.Equal(40, PlayerHealth());
    }

    [Fact]
    public void AnItemUseForAnotherPlayerIsSettledAsTheSender()
    {
        SetUpRoom();
        MoveTo(0f, 0f);

        SetHealth(10);
        Carry(EInstantItemType.HealthKit);

        // The message names somebody else, in both spellings the client uses.
        // The server stamps the connection's own player over both before the
        // handler sees them, so there is no field left to forge: an item is
        // always spent by the player whose connection carried the message.
        probe.Send(playerId, roomId, new JObject
        {
            ["MessageType"] = "ItemUseRequest",
            ["PlayerId"] = "someone-else",
            ["ItemType"] = "HealthKit",
            ["Effect"] = "heal"
        });
        Pump(400);

        // The sender's own kit was spent and the sender's own health moved. The
        // other player is not in the room, and nothing was taken from them.
        Assert.Equal(40, PlayerHealth());
        Assert.False(StillCarrying(EInstantItemType.HealthKit));
    }

    [Fact]
    public void TheSenderCannotSpendAnItemAnotherPlayerIsCarrying()
    {
        SetUpRoom();
        MoveTo(0f, 0f);

        SetHealth(10);
        // Nothing carried here, but the message claims a different player who,
        // if this were taken at face value, would have one.
        var otherId = "other-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        room!.AddNewPlayer(new PlayerInfo(otherId, "Other"));
        Assert.True(room.TryGetPlayer(otherId, out var other));
        other!.EquipInstantItems.Add(EInstantItemType.HealthKit);

        probe.Send(playerId, roomId, new JObject
        {
            ["MessageType"] = "ItemUseRequest",
            ["PlayerId"] = otherId,
            ["ItemType"] = "HealthKit",
            ["Effect"] = "heal"
        });
        Pump(400);

        // The named player keeps their kit and their health, and the sender, who
        // has none, gets nothing.
        Assert.Equal(10, PlayerHealth());
        Assert.Contains(EInstantItemType.HealthKit, other.EquipInstantItems);
        Assert.Equal(100, other.Health);
    }

    [Fact]
    public void AnUnknownItemIsInert()
    {
        SetUpRoom();
        MoveTo(0f, 0f);

        SetHealth(10);

        UseItem("NotAnItem");
        Pump(400);

        Assert.Equal(10, PlayerHealth());
    }

    [Fact]
    public void TheEffectIsWhatTheItemIsRatherThanWhatTheMessageSays()
    {
        // A kit heals. The message does not get to say otherwise, which is the
        // point of the rules living in the shared package.
        Assert.Equal(EInstantItemEffect.Heal, InstantItemRules.EffectOf(EInstantItemType.HealthKit));
        Assert.Equal(EInstantItemEffect.Ammo, InstantItemRules.EffectOf(EInstantItemType.FireBullet));
        Assert.Null(InstantItemRules.EffectOf(EInstantItemType.None));
        Assert.False(InstantItemRules.IsUsable(EInstantItemType.None));
    }

    // ---- Weapon reservation ---------------------------------------------

    /// <summary>
    /// A second realtime client, for the cases where one player has to be kept
    /// out of something another player is doing.
    /// <para>
    /// A reservation is only meaningful between players: the whole point of one
    /// is that somebody else is refused. A test with a single client could not
    /// tell a reservation that works from one that does nothing.
    /// </para>
    /// </summary>
    private MatchRudpProbe JoinSecondPlayer(out string secondId)
    {
        secondId = "rival-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        room!.AddNewPlayer(new PlayerInfo(secondId, "Rival"));
        MatchServerV2.Instance.ServerLagCompensationManager.AddPlayer(secondId);

        var token = server.IssueConnectionToken(secondId);
        Assert.False(string.IsNullOrWhiteSpace(token), $"no token was issued for {secondId}");

        var rival = new MatchRudpProbe();
        rival.Listener.NetworkReceiveEvent += (peer, reader, method) =>
        {
            var payload = reader.GetString();
            reader.Recycle();
            try
            {
                rival.Offer(JObject.Parse(payload));
            }
            catch (Exception)
            {
                // Test only; malformed payloads are ignored.
            }
        };

        Assert.True(
            rival.Connect("127.0.0.1", server.UdpPort ?? 0, secondId, token, roomId, 5000, () => server.PollingEvent()),
            $"the second realtime client for {secondId} did not connect");

        return rival;
    }

    /// <summary>
    /// Claims a weapon, as FieldWeaponController does, naming it by type.
    /// </summary>
    private void Reserve(MatchRudpProbe from, string actorId, string messageType = "WeaponReserve")
    {
        from.Send(actorId, roomId, new JObject
        {
            ["MessageType"] = messageType,
            ["WeaponType"] = "Rifle"
        });
    }

    private string ReservedBy()
    {
        var weapon = WeaponItem();
        return weapon?["ReservedByPlayerId"]?.ToString() ?? "";
    }

    [Fact]
    public void AWeaponClaimedOverRealtimeIsRecordedOnTheServer()
    {
        SetUpRoom();
        MoveTo(3f, 0f);
        DropWeapon("Rifle", magazine: 8);
        Pump(400);

        Assert.Equal("", ReservedBy());

        Reserve(probe, playerId);
        Pump(400);

        // A claim used to be a field the client set on its own copy and relayed
        // to the other clients, so the server never knew who had laid claim to
        // anything. This is the assertion that it does now.
        Assert.Equal(playerId, ReservedBy());
    }

    [Fact]
    public void AClaimedWeaponCannotBeTakenByAnotherPlayer()
    {
        SetUpRoom();
        MoveTo(3f, 0f);
        DropWeapon("Rifle", magazine: 8);
        Pump(400);

        var rival = JoinSecondPlayer(out var rivalId);
        try
        {
            // Both standing where the weapon is, which is the case the claim is
            // for: two players reaching for the same thing.
            for (var i = 0; i < 5; i++)
            {
                rival.SendPosition(rivalId, roomId, 3f, 0f, 0f, 0f, 0.04f, (byte)(i + 1));
                Pump(60);
            }

            Reserve(probe, playerId);
            Pump(400);
            Assert.Equal(playerId, ReservedBy());

            var weaponId = WeaponItem()?["ItemId"]?.ToString() ?? "";

            // The rival claims it too, and then tries to take it. A client could
            // previously do this because there was no claim to honour: two
            // players would both end up believing they had the weapon.
            Reserve(rival, rivalId);
            Pump(400);
            rival.Send(rivalId, roomId, new JObject
            {
                ["MessageType"] = "ItemPickup",
                ["ItemId"] = weaponId
            });
            Pump(400);

            Assert.True(
                MatchRoomManager.Instance.GetFieldItemManager(roomId)!.TryGetItem(weaponId, out var taken),
                "the claimed weapon vanished");
            Assert.Equal("Spawned", taken!.State);
        }
        finally
        {
            rival.Dispose();
        }
    }

    [Fact]
    public void AClaimCanBeReleasedSoTheWeaponIsFreeAgain()
    {
        SetUpRoom();
        MoveTo(3f, 0f);
        DropWeapon("Rifle", magazine: 8);
        Pump(400);

        Reserve(probe, playerId);
        Pump(400);
        Assert.Equal(playerId, ReservedBy());

        Reserve(probe, playerId, "WeaponRelease");
        Pump(400);

        Assert.Equal("", ReservedBy());
    }

    [Fact]
    public void AClaimFromAPlayerWithNoPositionIsRefused()
    {
        SetUpRoom();
        DropWeapon("Rifle", magazine: 8);
        Pump(400);

        // The claim says which weapon, but standing next to it is the server's
        // half of the claim, and there is nowhere the player is standing.
        Reserve(probe, playerId);
        Pump(400);

        Assert.Equal("", ReservedBy());
    }

    [Fact]
    public void AClaimOnAWeaponAcrossTheMapIsRefused()
    {
        SetUpRoom();
        MoveTo(3f, 0f);
        DropWeapon("Rifle", magazine: 8);
        Pump(400);

        // A weapon elsewhere entirely. Without the reach check, a player could
        // claim one on the far side of the map and lock it for everybody standing
        // there, so walk away from it and then try to claim it.
        MoveTo(30f, 0f);
        Reserve(probe, playerId);
        Pump(400);

        Assert.Equal("", ReservedBy());
    }

    [Fact]
    public void AClaimOnAWeaponThatIsNotThereIsInert()
    {
        SetUpRoom();
        MoveTo(3f, 0f);

        Reserve(probe, playerId);
        Pump(400);

        Assert.Null(WeaponItem());
    }

    [Fact]
    public void AClaimSurvivesAStateRoundTrip()
    {
        SetUpRoom();
        MoveTo(3f, 0f);
        DropWeapon("Rifle", magazine: 8);
        Pump(400);

        Reserve(probe, playerId);
        Pump(400);

        // The claim is state the server holds, so it has to survive a sync like
        // the magazine does. A claim that came back empty would leave a weapon
        // anybody could walk off with.
        var items = MatchRoomManager.Instance.GetFieldItemManager(roomId)!;
        items.LoadFromJson(items.ToJson());

        Assert.Equal(playerId, ReservedBy());
    }

    // ---- The ruling the client reads back --------------------------------

    [Fact]
    public void AGrantedPickupIsAnsweredUnderTheNameTheClientReads()
    {
        SetUpRoom();
        var items = MatchRoomManager.Instance.GetFieldItemManager(roomId)!;
        var itemId = items.SpawnItem(EFieldItemType.PowerUpItem, 0f, 0f, 0f);

        MoveTo(0f, 0f);

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            ClaimPickup(itemId);
            Pump(400);
        });

        // The client applies a pickup before the server rules on it and keeps a
        // revert for a refusal, so the ruling is the only thing that tells it
        // whether the effect it applied should stand. It arrived under a second
        // literal the client did not dispatch on, so every granted pickup was
        // taken back by the client's own timeout.
        var ruling = recorder.TheRuling(MessageType.FieldItemPickup);
        Assert.Equal(itemId, ruling["ItemId"]?.ToString());
        Assert.True(ruling["Success"]?.Value<bool>());
    }

    [Fact]
    public void ARefusedPickupIsAnsweredRatherThanLeftInSilence()
    {
        SetUpRoom();
        var items = MatchRoomManager.Instance.GetFieldItemManager(roomId)!;
        var itemId = items.SpawnItem(EFieldItemType.PowerUpItem, 500f, 0f, 0f);

        MoveTo(0f, 0f);

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            ClaimPickup(itemId);
            Pump(400);
        });

        // A refusal used to be logged and dropped, so the client that had
        // already applied the effect had to wait out its own timeout to find out
        // the server had said no.
        var ruling = recorder.TheRuling(MessageType.FieldItemPickup);
        Assert.Equal(itemId, ruling["ItemId"]?.ToString());
        Assert.False(ruling["Success"]?.Value<bool>());
    }

    [Fact]
    public void AClaimedWeaponIsRuledOnSoTheOtherClientsCanHonourIt()
    {
        SetUpRoom();
        MoveTo(3f, 0f);
        DropWeapon("Rifle", magazine: 8);
        Pump(400);

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Reserve(probe, playerId);
            Pump(400);
        });

        // The server holding the claim is not the same as anybody honouring it.
        // The claim used to be relayed by the client that made it, and the ruling
        // came back under a label no client dispatched on, so a reservation the
        // server accepted changed nobody's behaviour and two players could still
        // walk off with the same weapon.
        var ruling = recorder.TheRuling(MessageType.WeaponReserved);
        Assert.Equal(playerId, ruling["ReservedByPlayerId"]?.ToString());
    }

    [Fact]
    public void AWeaponRulingNamesWhereTheWeaponIsSoAClientCanFindIt()
    {
        SetUpRoom();
        MoveTo(3f, 0f);
        DropWeapon("Rifle", magazine: 8);
        Pump(400);

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Reserve(probe, playerId);
            Pump(400);
        });

        // A client matches a weapon to one on its screen by type and position,
        // so a ruling without a position names a weapon nothing can locate and
        // the claim is still not honoured.
        var ruling = recorder.TheRuling(MessageType.WeaponReserved);
        Assert.Equal("Rifle", ruling["WeaponType"]?.ToString());
        Assert.Equal(3f, ruling["PosX"]?.Value<float>());
    }

    [Fact]
    public void AReleasedWeaponIsRuledOnSoTheOthersStopHonouringTheClaim()
    {
        SetUpRoom();
        MoveTo(3f, 0f);
        DropWeapon("Rifle", magazine: 8);
        Pump(400);

        Reserve(probe, playerId);
        Pump(400);

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            Reserve(probe, playerId, messageType: "WeaponRelease");
            Pump(400);
        });

        // Without a ruling for the release the other clients keep the claim they
        // were given, so a weapon nobody is holding stays locked to whoever
        // reached for it first.
        var ruling = recorder.TheRuling(MessageType.WeaponReleased);
        Assert.Equal(playerId, ruling["ReservedByPlayerId"]?.ToString());
    }

    [Fact]
    public void ADroppedWeaponIsAnnouncedSoTheRestOfTheRoomCanSeeIt()
    {
        SetUpRoom();
        MoveTo(3f, 0f);

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            DropWeapon("Rifle", magazine: 8);
            Pump(400);
        });

        // A dropped weapon used to be something the dropping client drew for
        // itself, so it was on the ground for one player out of the room. The
        // announcement is what makes it a weapon anybody can pick up, and it has
        // to carry where it landed and what it is.
        var ruling = recorder.TheRuling(MessageType.WeaponDropped);
        Assert.Equal("Rifle", ruling["WeaponType"]?.ToString());
        Assert.Equal(3f, ruling["PosX"]?.Value<float>());
        Assert.Equal(8, ruling["MagazineAmmo"]?.Value<int>());
    }

    [Fact]
    public void ASpentInstantItemIsRuledOnWithTheHealthTheServerDecided()
    {
        SetUpRoom();
        MoveTo(0f, 0f);
        Carry(EInstantItemType.HealthKit);
        SetHealth(10);

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            UseItem(nameof(EInstantItemType.HealthKit), effect: "heal");
            Pump(400);
        });

        // The client spends the item on its own screen, so it has already shown
        // the heal it guessed at. The ruling carries what the server decided, and
        // the client adopts that, which is the whole point of the server owning
        // health. It used to be sent under the name the request used, so the
        // client read its own request back instead.
        var ruling = recorder.TheRuling(MessageType.ItemUsed);
        Assert.Equal(10 + InstantItemRules.HealAmount, ruling["Health"]?.Value<int>());
        Assert.Equal(InstantItemRules.HealAmount, ruling["RestoredHealth"]?.Value<int>());
    }

    [Fact]
    public void ARefusedInstantItemIsAnsweredWithTheHealthTheServerHolds()
    {
        SetUpRoom();
        MoveTo(0f, 0f);
        SetHealth(10);

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);

            // The player is carrying nothing, so the spend is turned down. The
            // client has already shown the heal on its own screen by this point.
            UseItem(nameof(EInstantItemType.HealthKit), effect: "heal");
            Pump(400);
        });

        // A refusal used to be logged only, so the client went on showing a heal
        // that never happened until the next shot corrected it.
        var ruling = recorder.TheRuling(MessageType.ItemUseRefused);
        Assert.Equal(10, ruling["Health"]?.Value<int>());
        Assert.Equal(0, ruling["RestoredHealth"]?.Value<int>());
    }

    [Fact]
    public void AFieldItemSpawnIsAnnouncedUnderTheNameTheClientDispatchesOn()
    {
        SetUpRoom();

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            FieldItemEventHandler.SpawnItem(
                MatchRoomManager.Instance.GetFieldItemManager(roomId)!,
                EFieldItemType.PowerUpItem,
                2f, 3f, 0f,
                message => GameMessageDispatcher.BroadcastToRoom(roomId, message));
        });

        // A spawn and a despawn had three spellings across the two sides and
        // neither pair matched, so an item the server put on the map reached a
        // client under no name it dispatched on. The client finds an item by the
        // name the server gave it, so the id has to travel with the announcement.
        var ruling = recorder.TheRuling(MessageType.FieldItemSpawn);
        Assert.False(string.IsNullOrWhiteSpace(ruling["ItemId"]?.ToString()));
        Assert.Equal(nameof(EFieldItemType.PowerUpItem), ruling["ItemType"]?.ToString());
        Assert.Equal(2f, ruling["PositionX"]?.Value<float>());
    }

    [Fact]
    public void AFieldItemDespawnIsAnnouncedUnderTheNameTheClientDispatchesOn()
    {
        SetUpRoom();

        var items = MatchRoomManager.Instance.GetFieldItemManager(roomId)!;
        var itemId = items.SpawnItem(EFieldItemType.PowerUpItem, 0f, 0f, 0f);

        var recorder = new BroadcastRecorder();
        BroadcastRecorder.During(() =>
        {
            GameMessageDispatcher.Initialize(recorder);
            FieldItemEventHandler.DespawnItem(
                items,
                itemId,
                message => GameMessageDispatcher.BroadcastToRoom(roomId, message));
        });

        // The client removes the item by the name the server gave it, so the
        // announcement has to carry that name. Removing it by position instead
        // would take down whichever item happened to be nearest.
        var ruling = recorder.TheRuling(MessageType.FieldItemDespawn);
        Assert.Equal(itemId, ruling["ItemId"]?.ToString());
    }
}
