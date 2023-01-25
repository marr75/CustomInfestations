using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CustomInfestations; 

[HarmonyPatch(typeof(CompSpawnerHives))]
public static class MyCompSpawnerHives {
    [HarmonyPostfix]
    [HarmonyPatch("CanSpawnHiveAt")]
    static void PostCanSpawnHiveAt(IntVec3 c, Map map, ref bool __result) {
        __result = __result 
            && c.GetThingList(map).All(thing => thing.def.category != ThingCategory.Building) 
            && !map.terrainGrid.TerrainAt(c).HasTag("NoInfestation");
    }
}

[HarmonyPatch(typeof(InfestationCellFinder))]
public static class MyInfestationCellFinder {
    
    static IEnumerable<Tuple<IntVec3, Material>> _cellMaterialCache;

    [HarmonyReversePatch]
    [HarmonyPatch("CellHasBlockingThings")]
    static bool CellHasBlockingThings(IntVec3 cell, Map map) {
        throw new NotImplementedException();
    }

    [HarmonyReversePatch]
    [HarmonyPatch("GetMountainousnessScoreAt")]
    static float GetMountainousnessScoreAt(IntVec3 cell, Map map) {
        throw new NotImplementedException();
    }
    
    [HarmonyReversePatch]
    [HarmonyPatch("StraightLineDistToUnroofed")]
    static int StraightLineDistToUnroofed(IntVec3 cell, Map map) {
        throw new NotImplementedException();
    }
    
    [HarmonyReversePatch]
    [HarmonyPatch("DistToBlocker")]
    static float DistToBlocker(IntVec3 cell, Map map) {
        throw new NotImplementedException();
    }
    
    [HarmonyReversePatch]
    [HarmonyPatch("CalculateTraversalDistancesToUnroofed")]
    static void CalculateTraversalDistancesToUnroofed(Map map){
        throw new NotImplementedException();
    }
    
    [HarmonyReversePatch]
    [HarmonyPatch("CalculateClosedAreaSizeGrid")]
    static void CalculateClosedAreaSizeGrid(Map map){
        throw new NotImplementedException();
    }

    [HarmonyReversePatch]
    [HarmonyPatch("CalculateDistanceToColonyBuildingGrid")]
    static void CalculateDistanceToColonyBuildingGrid(Map map) {
        throw new NotImplementedException();
    }

    static float GetScoreAtInternal(
        IntVec3 cell,
        Map map,
        ByteGrid distToColonyBuilding,
        ByteGrid closedAreaSize,
        IReadOnlyDictionary<Region, float> regionsDistanceToUnroofed
    ) {
        var region = cell.GetRegion(map);

        #if DEBUG

        var things = map.thingGrid.ThingsAt(cell).ToArray();
        var tags = map.terrainGrid.TerrainAt(cell).tags ?? Enumerable.Empty<string>();
        var test = new {
            X = cell.x,
            Z = cell.z,
            Initialized = distToColonyBuilding != null
                && region != null
                && closedAreaSize != null
                && regionsDistanceToUnroofed != null,
            DistanceToColonyBuilding = distToColonyBuilding != null ? distToColonyBuilding[cell] : -1,
            Walkable = cell.Walkable(map),
            Fogged = map.fogGrid.IsFogged(cell),
            Blocked = CellHasBlockingThings(cell, map),
            ThingString = string.Join(", ", things.Select(t => t.def.defName)),
            HasBuildings = things.Any(thing => thing.def.category == ThingCategory.Building),
            Tags = string.Join(", ", tags),
            Roof = map.roofGrid.RoofAt(cell),
            ClosedArea = closedAreaSize != null ? closedAreaSize[cell] : -1,
        };
        
        #endif
        
        var inappropriateCell = distToColonyBuilding == null
            || region == null
            || closedAreaSize == null
            || regionsDistanceToUnroofed == null
            || distToColonyBuilding[cell] > Settings.MinimumDistanceToColonyBuilding
            || !cell.Walkable(map)
            || cell.Fogged(map)
            || CellHasBlockingThings(cell, map)
            || (
                Settings.ForbidOnBuildings
                && cell.GetThingList(map).Any(thing => thing.def.category == ThingCategory.Building)
            )
            || map.terrainGrid.TerrainAt(cell).HasTag("NoInfestation")
            || !cell.Roofed(map)
            || !cell.GetRoof(map).isThickRoof
            || closedAreaSize[cell] < Settings.SmallestClosedArea;

        if (inappropriateCell)
            return 0f;
        
        var temperature = cell.GetTemperature(map);
        if (temperature >= Settings.PossibleTemperatureRangeMax || temperature <= Settings.PossibleTemperatureRangeMin)
            return 0f;
        var temperatureFactor = (temperature < Settings.SafeTemperatureRangeMin,  temperature > Settings.SafeTemperatureRangeMax) switch {
            (true, false) => Mathf.InverseLerp(Settings.PossibleTemperatureRangeMin, Settings.SafeTemperatureRangeMin, temperature),
            (false, true) => Mathf.InverseLerp(Settings.SafeTemperatureRangeMax, Settings.PossibleTemperatureRangeMax, temperature),
            _ => 1.0f,
        };

        var brightness = map.glowGrid.GameGlowAt(cell);
        var brightnessFactor = 1.0f - (Mathf.InverseLerp(0f, Settings.BrightestLightClamp, brightness) * Settings.MaximumLightAttenuation);
        if (brightnessFactor <= 0)
            return 0f;
        
        var mountainFactor = GetMountainousnessScoreAt(cell, map);
        if (mountainFactor <= 0)
            return 0f;
        
        var cardinalDistanceToOpening = StraightLineDistToUnroofed(cell, map);
        var regionalDistanceToOpeningFactor = Mathf.Pow(
            regionsDistanceToUnroofed.TryGetValue(region, out var a) 
                ? Mathf.Min(a, cardinalDistanceToOpening * 4f) 
                : cardinalDistanceToOpening * 1.15f,
            1.55f
        );
        var cardinalDistanceToOpeningFactor = Mathf.InverseLerp(0.0f, 12f, cardinalDistanceToOpening);

        var nearestBlocker = DistToBlocker(cell, map);
        var nearestBlockerFactor = 1f - Mathf.Clamp(nearestBlocker / 11f, 0.0f, 0.6f);
        if (nearestBlockerFactor <= 0)
            return 0f;
        
        var score = (
            regionalDistanceToOpeningFactor 
            * cardinalDistanceToOpeningFactor 
            * nearestBlockerFactor 
            * mountainFactor 
            * brightnessFactor 
            * temperatureFactor
        );

        return score < Settings.MinimumScore ? 0f : score;
    }

    [HarmonyPrefix]
    [HarmonyPatch("GetScoreAt")]
    static bool PreGetScoreAt(
        IntVec3 cell,
        Map map,
        ByteGrid ___distToColonyBuilding,
        ByteGrid ___closedAreaSize,
        Dictionary<Region, float> ___regionsDistanceToUnroofed,
        ref float __result
    ) {
        const bool continueExecution = false;
        __result = GetScoreAtInternal(cell, map, ___distToColonyBuilding, ___closedAreaSize, ___regionsDistanceToUnroofed);
        return continueExecution; // skip execution
    }

    [HarmonyPrefix]
    [HarmonyPatch("DebugDraw")]
    static bool PreDebugDraw(
        ByteGrid ___distToColonyBuilding,
        ByteGrid ___closedAreaSize,
        Dictionary<Region, float> ___regionsDistanceToUnroofed
    ) { 
            if (DebugViewSettings.drawInfestationChance && Time.frameCount % 60 == 0)
            {
                var currentMap = Find.CurrentMap;
                var cellRect = Find.CameraDriver.CurrentViewRect;
                cellRect = cellRect.ExpandedBy(1);
                cellRect.ClipInsideMap(currentMap);
                CalculateTraversalDistancesToUnroofed(currentMap);
                CalculateClosedAreaSizeGrid(currentMap);
                CalculateDistanceToColonyBuildingGrid(currentMap);
                var cells = cellRect.Cells.ToArray();
                var scores = cells.Select(
                    cell => new {
                        Cell = cell,
                        Score = GetScoreAtInternal(
                            cell,
                            currentMap,
                            ___distToColonyBuilding,
                            ___closedAreaSize,
                            ___regionsDistanceToUnroofed
                        ),
                    }
                ).Where(cs => cs.Score > Settings.MinimumScore).ToArray();
                var maxScore = scores.NullOrEmpty() ? 0 : scores.Max(cs => cs.Score);
                Material ColorMaker(float score) => SolidColorMaterials.SimpleSolidColorMaterial(
                    new Color(
                        0.0f,
                        0.0f,
                        1f,
                        GenMath.LerpDouble(
                            Settings.MinimumScore,
                            maxScore,
                            0.0f,
                            1f,
                            score
                        )
                    )
                );
                var cellColors = scores.Select(
                    cs => new Tuple<IntVec3, Material>(cs.Cell, ColorMaker(cs.Score))
                );
                _cellMaterialCache = cellColors.ToArray();
            }
            
            if (DebugViewSettings.drawInfestationChance && _cellMaterialCache is not null) {
                foreach (var cellMaterialPair in _cellMaterialCache) {
                    var shiftedWithAltitude = cellMaterialPair.Item1.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays);
                    Graphics.DrawMesh(MeshPool.plane10, shiftedWithAltitude, Quaternion.identity, cellMaterialPair.Item2, 0);
                }
            }

            const bool continueExecution = false;
            return continueExecution; // Override the original
    }
}
