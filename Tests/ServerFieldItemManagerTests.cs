using System.Linq;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network;
using Xunit;

namespace OpenGSServer.Tests;

/// <summary>
/// Covers the field item manager: spawn limits, pickup authority, and the json
/// round trip.
/// <para>
/// Item types used to be free form strings, so a typo in a spawn rule silently
/// produced an item nothing could pick up and nothing failed loudly. The type
/// is now EFieldItemType and the wire name is produced in one place, and these
/// tests hold that contract down.
/// </para>
/// </summary>
public sealed class ServerFieldItemManagerTests
{
    private static ServerFieldItemManager NewManager()
    {
        var manager = new ServerFieldItemManager();
        manager.StartMatch("room-1");
        return manager;
    }

    private static ServerFieldItemManager NewManagerWithPoint()
    {
        var manager = NewManager();
        manager.RegisterSpawnPoint(0, 4f, 0f, 0f, "Center");
        return manager;
    }

    // ---- Wire names -----------------------------------------------------

    [Theory]
    [InlineData("PowerUpItem", EFieldItemType.PowerUpItem)]
    [InlineData("DefenceUpItem", EFieldItemType.DefenceUpItem)]
    [InlineData("SpeedUpItem", EFieldItemType.SpeedUpItem)]
    [InlineData("StealthItem", EFieldItemType.StealthItem)]
    [InlineData("GrenadePack", EFieldItemType.GrenadePack)]
    [InlineData("HealItem", EFieldItemType.HealItem)]
    [InlineData("GranadeLauncher", EFieldItemType.GranadeLauncher)]
    [InlineData("FlameThrower", EFieldItemType.FlameThrower)]
    public void CanonicalNamesRoundTrip(string wire, EFieldItemType expected)
    {
        Assert.True(FieldItemTypeNames.TryParse(wire, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(wire, FieldItemTypeNames.ToWireName(parsed));
    }

    [Theory]
    [InlineData("PowerUp", EFieldItemType.PowerUpItem)]
    [InlineData("DefenceUp", EFieldItemType.DefenceUpItem)]
    [InlineData("SpeedUp", EFieldItemType.SpeedUpItem)]
    [InlineData("Stealth", EFieldItemType.StealthItem)]
    [InlineData("NormalGrenadePack", EFieldItemType.GrenadePack)]
    [InlineData("RocketLauncher", EFieldItemType.GranadeLauncher)]
    public void LegacyClientNamesStillParse(string legacy, EFieldItemType expected)
    {
        // The client and the server never agreed on the spelling of these, so
        // both are still accepted on the way in. Writes always use the
        // canonical name.
        Assert.True(FieldItemTypeNames.TryParse(legacy, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NotAnItem")]
    [InlineData("99")]
    public void UnknownNamesAreRejectedRatherThanDefaulted(string wire)
    {
        Assert.False(FieldItemTypeNames.TryParse(wire, out _));
    }

    [Fact]
    public void EveryEnumValueHasANameThatParsesBack()
    {
        foreach (var type in FieldItemTypeNames.All)
        {
            Assert.True(FieldItemTypeNames.TryParse(FieldItemTypeNames.ToWireName(type), out var parsed));
            Assert.Equal(type, parsed);
        }
    }

    [Fact]
    public void TimedBuffsAreDistinguishedFromOneShotItems()
    {
        Assert.True(FieldItemTypeNames.IsTimedBuff(EFieldItemType.PowerUpItem));
        Assert.True(FieldItemTypeNames.IsTimedBuff(EFieldItemType.StealthItem));
        Assert.False(FieldItemTypeNames.IsTimedBuff(EFieldItemType.HealItem));
        Assert.False(FieldItemTypeNames.IsTimedBuff(EFieldItemType.GrenadePack));
        Assert.False(FieldItemTypeNames.IsTimedBuff(EFieldItemType.GranadeLauncher));
    }

    // ---- Spawning -------------------------------------------------------

    [Fact]
    public void SpawnItemCreatesAnActiveItemThatCanBeFound()
    {
        var manager = NewManager();

        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 1f, 2f, 3f);

        Assert.NotEmpty(itemId);
        Assert.True(manager.TryGetItem(itemId, out var item));
        Assert.Equal(EFieldItemType.HealItem, item!.ItemType);
        Assert.True(item.IsActive);
        Assert.Equal("Spawned", item.State);
        Assert.Equal(1, manager.GetActiveItemCount());
        Assert.Equal(1, manager.GetActiveItemCountForType(EFieldItemType.HealItem));
    }

    [Fact]
    public void SpawnItemRejectsNonFinitePositions()
    {
        var manager = NewManager();

        Assert.Equal(string.Empty, manager.SpawnItem(EFieldItemType.HealItem, float.NaN, 0f, 0f));
        Assert.Equal(string.Empty, manager.SpawnItem(EFieldItemType.HealItem, 0f, float.PositiveInfinity, 0f));
        Assert.Equal(0, manager.GetActiveItemCount());
    }

    [Fact]
    public void EachSpawnedItemGetsADistinctId()
    {
        var manager = NewManager();

        var first = manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);
        var second = manager.SpawnItem(EFieldItemType.HealItem, 5f, 0f, 0f);

        Assert.NotEqual(first, second);
        Assert.Equal(2, manager.GetActiveItemCount());
    }

    [Fact]
    public void SpawnRuleCapsHowManyOfATypeCanBeActive()
    {
        var manager = NewManagerWithPoint();
        manager.ConfigureSpawnRule(EFieldItemType.HealItem, maxActiveCount: 1, respawnDelaySec: 10f, 0);

        var first = manager.SpawnItem(EFieldItemType.HealItem, 0);
        var second = manager.SpawnItem(EFieldItemType.HealItem, 0);

        Assert.NotEmpty(first);
        Assert.Equal(string.Empty, second);
        Assert.Equal(1, manager.GetActiveItemCountForType(EFieldItemType.HealItem));
    }

    [Fact]
    public void SpawnRuleLimitIsPerTypeNotGlobal()
    {
        var manager = NewManagerWithPoint();
        manager.ConfigureSpawnRule(EFieldItemType.HealItem, 1, 10f, 0);
        manager.ConfigureSpawnRule(EFieldItemType.PowerUpItem, 1, 10f, 0);

        Assert.NotEmpty(manager.SpawnItem(EFieldItemType.HealItem, 0));
        Assert.NotEmpty(manager.SpawnItem(EFieldItemType.PowerUpItem, 0));
    }

    [Fact]
    public void SpawnRuleClampsNonsensicalValues()
    {
        var manager = NewManagerWithPoint();

        manager.ConfigureSpawnRule(EFieldItemType.HealItem, maxActiveCount: 0, respawnDelaySec: -5f, 0);

        // A max of zero would block the item forever and a negative delay is
        // meaningless, so both are pulled into range.
        Assert.NotEmpty(manager.SpawnItem(EFieldItemType.HealItem, 0));
        Assert.Equal(string.Empty, manager.SpawnItem(EFieldItemType.HealItem, 0));
    }

    [Fact]
    public void TrySpawnConfiguredItemSucceedsThroughTheRule()
    {
        var manager = NewManagerWithPoint();
        manager.ConfigureDefaultSpawnRules();

        Assert.True(manager.TrySpawnConfiguredItem(EFieldItemType.SpeedUpItem, out var itemId));
        Assert.NotEmpty(itemId);
    }

    // ---- Pickup ---------------------------------------------------------

    [Fact]
    public void PickupClaimsTheItemAndReportsTheType()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.PowerUpItem, 0f, 0f, 0f);
        EFieldItemType? reported = null;
        manager.OnItemPickedUp = (id, player, type) => reported = type;

        var picked = manager.PickupItem(itemId, "p-1");

        Assert.True(picked);
        Assert.Equal(EFieldItemType.PowerUpItem, reported);
        Assert.True(manager.TryGetItem(itemId, out var item));
        Assert.Equal("PickedUp", item!.State);
        Assert.Equal("p-1", item.PickedUpByPlayerId);
        Assert.False(item.IsActive);
    }

    [Fact]
    public void AnItemCanOnlyBePickedUpOnce()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        Assert.True(manager.PickupItem(itemId, "p-1"));
        Assert.False(manager.PickupItem(itemId, "p-2"));
    }

    [Fact]
    public void PickupRejectsUnknownItemsAndMissingArguments()
    {
        var manager = NewManager();

        Assert.False(manager.PickupItem("nope", "p-1"));
        Assert.False(manager.PickupItem("", "p-1"));
        Assert.False(manager.PickupItem("id", ""));
    }

    [Fact]
    public void PickupOfADespawnedItemFails()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        manager.DespawnItem(itemId);

        Assert.False(manager.PickupItem(itemId, "p-1"));
    }

    [Fact]
    public void PickupFiresTheCallbackOnlyOnce()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);
        var calls = 0;
        manager.OnItemPickedUp = (_, _, _) => calls++;

        manager.PickupItem(itemId, "p-1");
        manager.PickupItem(itemId, "p-1");

        Assert.Equal(1, calls);
    }

    // ---- Despawn and lifecycle ------------------------------------------

    [Fact]
    public void DespawnRemovesTheItemFromPlay()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        manager.DespawnItem(itemId);

        Assert.Equal(0, manager.GetActiveItemCount());
    }

    [Fact]
    public void DespawnAllClearsEveryItem()
    {
        var manager = NewManager();
        manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);
        manager.SpawnItem(EFieldItemType.PowerUpItem, 1f, 0f, 0f);

        manager.DespawnAllItems();

        Assert.Equal(0, manager.GetActiveItemCount());
    }

    [Fact]
    public void StartMatchClearsItemsLeftOverFromThePreviousMatch()
    {
        var manager = NewManager();
        manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        manager.StartMatch("room-2");

        Assert.Equal(0, manager.GetActiveItemCount());
    }

    [Fact]
    public void EndMatchClearsItems()
    {
        var manager = NewManager();
        manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        manager.EndMatch();

        Assert.Equal(0, manager.GetActiveItemCount());
    }

    // ---- Json round trip ------------------------------------------------

    [Fact]
    public void JsonUsesTheCanonicalItemTypeName()
    {
        var manager = NewManager();
        manager.SpawnItem(EFieldItemType.PowerUpItem, 1f, 2f, 3f);

        var entry = (JObject)manager.ToJson()[0]!;

        Assert.Equal("PowerUpItem", entry["ItemType"]?.ToString());
        Assert.Equal(1f, entry["PositionX"]?.Value<float>());
        Assert.Equal(2f, entry["PositionY"]?.Value<float>());
        Assert.Equal(3f, entry["PositionZ"]?.Value<float>());
    }

    [Fact]
    public void JsonRoundTripPreservesEveryItemType()
    {
        var source = NewManager();
        foreach (var type in FieldItemTypeNames.All)
        {
            source.SpawnItem(type, 1f, 1f, 1f);
        }

        var restored = new ServerFieldItemManager();
        restored.LoadFromJson(source.ToJson());

        Assert.Equal(FieldItemTypeNames.All.Count, restored.GetActiveItemCount());
        foreach (var type in FieldItemTypeNames.All)
        {
            Assert.Equal(1, restored.GetActiveItemCountForType(type));
        }
    }

    [Fact]
    public void JsonRoundTripPreservesPickupState()
    {
        var source = NewManager();
        var itemId = source.SpawnItem(EFieldItemType.GrenadePack, 0f, 0f, 0f);
        source.PickupItem(itemId, "p-7");

        var restored = new ServerFieldItemManager();
        restored.LoadFromJson(source.ToJson());

        Assert.True(restored.TryGetItem(itemId, out var item));
        Assert.Equal("PickedUp", item!.State);
        Assert.Equal("p-7", item.PickedUpByPlayerId);
    }

    [Fact]
    public void LoadSkipsEntriesWithAnUnknownItemType()
    {
        var restored = new ServerFieldItemManager();

        restored.LoadFromJson(new JArray
        {
            JObject.Parse("{\"ItemId\":\"a\",\"ItemType\":\"HealItem\",\"PositionX\":1,\"PositionY\":1,\"PositionZ\":1}"),
            JObject.Parse("{\"ItemId\":\"b\",\"ItemType\":\"NotAnItem\",\"PositionX\":1,\"PositionY\":1,\"PositionZ\":1}"),
            JObject.Parse("{\"ItemId\":\"c\",\"PositionX\":1,\"PositionY\":1,\"PositionZ\":1}")
        });

        // A missing or unknown type used to fall back to a default item, so a
        // bad payload could invent a pickup. Unknown entries are dropped.
        Assert.Equal(1, restored.GetActiveItemCount());
    }

    [Fact]
    public void LoadAcceptsTheLegacyItemTypeSpellings()
    {
        var restored = new ServerFieldItemManager();

        restored.LoadFromJson(new JArray
        {
            JObject.Parse("{\"ItemId\":\"a\",\"ItemType\":\"PowerUp\",\"PositionX\":0,\"PositionY\":0,\"PositionZ\":0}"),
            JObject.Parse("{\"ItemId\":\"b\",\"ItemType\":\"RocketLauncher\",\"PositionX\":0,\"PositionY\":0,\"PositionZ\":0}")
        });

        Assert.Equal(1, restored.GetActiveItemCountForType(EFieldItemType.PowerUpItem));
        Assert.Equal(1, restored.GetActiveItemCountForType(EFieldItemType.GranadeLauncher));
    }

    [Fact]
    public void LoadSkipsEntriesWithNonFinitePositions()
    {
        var restored = new ServerFieldItemManager();

        restored.LoadFromJson(new JArray
        {
            JObject.Parse("{\"ItemId\":\"a\",\"ItemType\":\"HealItem\",\"PositionX\":1e40,\"PositionY\":0,\"PositionZ\":0}")
        });

        Assert.Equal(0, restored.GetActiveItemCount());
    }

    [Fact]
    public void LoadReplacesRatherThanMergesTheExistingItems()
    {
        var manager = NewManager();
        manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        manager.LoadFromJson(new JArray());

        Assert.Equal(0, manager.GetActiveItemCount());
    }
    // ---- Authoritative pickup ------------------------------------------

    [Fact]
    public void PickupIsGrantedWhenThePlayerStandsOnTheItem()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 5f, 0f, 0f);

        Assert.True(manager.PickupItem(itemId, "p-1", 5f, 0f, 0f));
    }

    [Fact]
    public void PickupIsGrantedInsideTheRadius()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 5f, 0f, 0f);
        var offset = ServerFieldItemManager.PickupRadius - 0.1f;

        Assert.True(manager.IsWithinPickupRange(itemId, 5f - offset, 0f, 0f));
    }

    [Fact]
    public void PickupIsRefusedFromAcrossTheMap()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 5f, 0f, 0f);

        // The client used to be able to claim any item by id alone.
        Assert.False(manager.IsWithinPickupRange(itemId, 500f, 0f, 0f));
        Assert.False(manager.PickupItem(itemId, "p-1", 500f, 0f, 0f));

        Assert.True(manager.TryGetItem(itemId, out var item));
        Assert.Equal("Spawned", item!.State);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void PickupDistanceIsMeasuredInThreeDimensions()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        // Close on the plane, far above it.
        Assert.False(manager.IsWithinPickupRange(itemId, 0f, 0f, 100f));
    }

    [Fact]
    public void PickupRangeCheckRejectsNonFinitePositions()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        Assert.False(manager.IsWithinPickupRange(itemId, float.NaN, 0f, 0f));
        Assert.False(manager.IsWithinPickupRange(itemId, float.PositiveInfinity, 0f, 0f));
    }

    [Fact]
    public void PickupRangeCheckRejectsAnUnknownItem()
    {
        var manager = NewManager();

        Assert.False(manager.IsWithinPickupRange("nope", 0f, 0f, 0f));
    }

    [Fact]
    public void APartialPositionIsRefusedRatherThanTreatedAsNoCheck()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        // Supplying only some of the coordinates is a client bug. Treating it as
        // "no position given" would hand out a free pass.
        Assert.False(manager.PickupItem(itemId, "p-1", 0f, null, null));
        Assert.True(manager.TryGetItem(itemId, out var item));
        Assert.Equal("Spawned", item!.State);
    }

    [Fact]
    public void NoPositionFallsBackToTheUnverifiedPath()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 500f, 0f, 0f);

        // Callers with no position to offer still work, and the test suite and
        // offline tools rely on that.
        Assert.True(manager.PickupItem(itemId, "p-1", null, null, null));
    }

    [Fact]
    public void ARefusedPickupDoesNotConsumeTheItem()
    {
        var manager = NewManager();
        var itemId = manager.SpawnItem(EFieldItemType.HealItem, 0f, 0f, 0f);

        Assert.False(manager.PickupItem(itemId, "p-1", 900f, 0f, 0f));

        // A player who walked over later still gets it.
        Assert.True(manager.PickupItem(itemId, "p-1", 0f, 0f, 0f));
    }
}