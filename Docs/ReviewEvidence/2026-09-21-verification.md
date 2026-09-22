# Unity verification — September 21, 2026

Unity 6000.5.10f1, macOS, EditMode: 279 tests; 270 passed; 9 failed; 0 skipped.

Executed against HEAD 6a8ea94 plus existing local edits. No gameplay code was fixed in this review. Results are not a full-player, Windows, performance or campaign acceptance test.

The interactive production roadmap passed a strict TypeScript check against the installed canvas SDK. Local links in both written documents were checked for existing targets.

## Failed cases

### SolarMajesty.Tests.CampusDressingTests.AirlockHub_IsSmallWhiteSquare_NoWrapDoors

```text
hub hull must be sheet-white
  Expected: greater than 0.879999995f
  But was:  0.734800041f
```

### SolarMajesty.Tests.CampusDressingTests.DensePack_WorkshopPrefersAirlockDock_BeforeYardsFillIt

```text
Expected: True
  But was:  False
```

### SolarMajesty.Tests.CampusDressingTests.GhostAirlock_ShowsAllArms_EachWithOneOrangeCollar

```text
hub hull must be sheet-white
  Expected: greater than 0.879999995f
  But was:  0.734800041f
```

### SolarMajesty.Tests.CampusDressingTests.LiveHab_ThickCarbonMidBand_AndOrangeRimHatches

```text
mid-band stays near-black
  Expected: less than 0.200000003f
  But was:  0.403699994f
```

### SolarMajesty.Tests.CampusDressingTests.RefreshTubes_EnablesOnlyDockedCommonsNorth

```text
hub hull must be sheet-white
  Expected: greater than 0.879999995f
  But was:  0.734800041f
```

### SolarMajesty.Tests.CampusDressingTests.RefreshTubes_FindsVillageRingAirlock

```text
Unhandled log message: '[Error] Dress_HubPlinth: Destroy may not be called from edit mode! Use DestroyImmediate instead.
Destroying an object in edit mode destroys it permanently'. Use UnityEngine.TestTools.LogAssert.Expect
```

### SolarMajesty.Tests.CampusDressingTests.RefreshTubes_SpacedCampus_EnablesDockedArmsWithoutTubeRuns

```text
Unhandled log message: '[Error] Dress_HubPlinth: Destroy may not be called from edit mode! Use DestroyImmediate instead.
Destroying an object in edit mode destroys it permanently'. Use UnityEngine.TestTools.LogAssert.Expect
```

### SolarMajesty.Tests.CampusDressingTests.SnapToGroundKeepingDockAxis_PreservesSharedDockY

```text
Unhandled log message: '[Error] Dress_HubPlinth: Destroy may not be called from edit mode! Use DestroyImmediate instead.
Destroying an object in edit mode destroys it permanently'. Use UnityEngine.TestTools.LogAssert.Expect
```

### SolarMajesty.Tests.CampusDressingTests.StillHabSocket_SpawnsFoundationAndCrane

```text
Unhandled log message: '[Error] SocketDisc: Destroy may not be called from edit mode! Use DestroyImmediate instead.
Destroying an object in edit mode destroys it permanently'. Use UnityEngine.TestTools.LogAssert.Expect
```
