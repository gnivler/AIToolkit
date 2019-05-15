﻿using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Rendering.UI;
using BattleTech.UI;
using BattleTech.UI.Tooltips;
using HBS;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnhancedAI.Features
{
    public static class InfluenceMapVisualization
    {
        private static GameObject _parent = new GameObject("InfluenceMapVisualizationParent");
        private static List<GameObject> _unusedDotPool = new List<GameObject>();
        private static List<GameObject> _usedDotPool = new List<GameObject>();
        private static Mesh _circleMesh = GenerateCircleMesh(4, 20);
        private static Vector3 _groundOffset = 2 * Vector3.up;


        public static void Show()
        {
            _parent.SetActive(true);
        }

        public static void OnInfluenceMapSort(AbstractActor unit)
        {
            Hide();

            var map = unit.BehaviorTree.influenceMapEvaluator;

            var lowest = float.MaxValue;
            var highest = float.MinValue;
            var total = 0f;
            var hexToHighestData = new Dictionary<MapTerrainDataCell, HexFactorData>();

            for (var i = 0; i < map.firstFreeWorkspaceEvaluationEntryIndex; i++)
            {
                var entry = map.WorkspaceEvaluationEntries[i];
                var hex = unit.Combat.MapMetaData.GetCellAt(entry.Position);
                var value = entry.GetHighestAccumulator();

                highest = Mathf.Max(highest, value);
                lowest = Mathf.Min(lowest, value);
                total += value;

                if (hexToHighestData.ContainsKey(hex) && hexToHighestData[hex].Value > value)
                    continue;

                var data = new HexFactorData
                {
                    FactorRecords = entry.ValuesByFactorName,
                    MoveType = entry.GetBestMoveType(),
                    Position = entry.Position,
                    Value = value
                };

                hexToHighestData[hex] = data;
            }

            var average = total / map.firstFreeWorkspaceEvaluationEntryIndex;
            foreach (var hexData in hexToHighestData.Values)
            {
                var darkGrey = new Color(.15f, .15f, .15f);
                var color = hexData.Value <= average
                    ? Color.Lerp(Color.red, darkGrey, (hexData.Value - lowest) / (average - lowest))
                    : Color.Lerp(darkGrey, Color.cyan, (hexData.Value - average) / (highest - average));

                ShowDotAt(hexData.Position, color, hexData);
            }
        }

        public static void Hide()
        {
            foreach (var dot in _usedDotPool)
                dot.SetActive(false);

            _unusedDotPool.AddRange(_usedDotPool);
            _usedDotPool.Clear();
            _parent.SetActive(false);

            DotTooltip.CompareAgainst = null;
        }


        private static void ShowDotAt(Vector3 location, Color color, HexFactorData factorData)
        {
            GameObject dot;
            if (_unusedDotPool.Count > 0)
            {
                dot = _unusedDotPool[0];
                _unusedDotPool.RemoveAt(0);
            }
            else
            {
                var movementDot = CombatMovementReticle.Instance.movementDotTemplate;

                dot = new GameObject($"dot_{_unusedDotPool.Count + _usedDotPool.Count}");
                dot.transform.SetParent(_parent.transform);

                var meshFilter = dot.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = _circleMesh;

                var meshRenderer = dot.AddComponent<MeshRenderer>();
                meshRenderer.material = movementDot.GetComponent<MeshRenderer>().sharedMaterial;
                meshRenderer.material.enableInstancing = false;
                meshRenderer.receiveShadows = false;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

                var collider = dot.AddComponent<CapsuleCollider>();
                collider.center = Vector3.zero;
                collider.radius = 4f;
                collider.height = 1f;
                collider.isTrigger = true;

                dot.AddComponent<DotTooltip>();
                dot.AddComponent<UISweep>();
            }

            _usedDotPool.Add(dot);
            dot.transform.position = location + _groundOffset;
            dot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var tooltip = dot.GetComponent<DotTooltip>();
            tooltip.FactorData = factorData;

            var renderer = dot.GetComponent<MeshRenderer>();
            renderer.material.color = color;

            dot.SetActive(true);
        }

        private static Mesh GenerateCircleMesh(float size, int numberOfPoints)
        {
            // from https://answers.unity.com/questions/944228/creating-a-smooth-round-flat-circle.html
            // not subject to license
            var angleStep = 360.0f / numberOfPoints;
            var vertexList = new List<Vector3>();
            var triangleList = new List<int>();
            var quaternion = Quaternion.Euler(0.0f, 0.0f, angleStep);

            vertexList.Add(new Vector3(0.0f, 0.0f, 0.0f));
            vertexList.Add(new Vector3(0.0f, size, 0.0f));
            vertexList.Add(quaternion * vertexList[1]);
            triangleList.Add(0);
            triangleList.Add(1);
            triangleList.Add(2);

            for (var i = 0; i < numberOfPoints - 1; i++)
            {
                triangleList.Add(0);
                triangleList.Add(vertexList.Count - 1);
                triangleList.Add(vertexList.Count);
                vertexList.Add(quaternion * vertexList[vertexList.Count - 1]);
            }
            var mesh = new Mesh();
            mesh.vertices = vertexList.ToArray();
            mesh.triangles = triangleList.ToArray();

            return mesh;
        }
    }

    public class HexFactorData
    {
        public Vector3 Position;
        public float Value;
        public MoveType MoveType;
        public Dictionary<string, EvaluationDebugLogRecord> FactorRecords;
    }

    public class DotTooltip : MonoBehaviour
    {
        public static HexFactorData CompareAgainst;
        public HexFactorData FactorData;
        private readonly HBSTooltipStateData _tooltipStateData = new HBSTooltipStateData();


        public void OnMouseEnter()
        {
            _tooltipStateData.SetString(GetTooltipString());
            LazySingletonBehavior<TooltipManager>.Instance.SpawnTooltip(
                _tooltipStateData.GetTooltipObject(), 792402, 792402,
                0f, _tooltipStateData.GetPrefabOverride(), true);
        }

        public void OnMouseExit()
        {
            LazySingletonBehavior<TooltipManager>.Instance.ClearTooltip();
        }

        public void OnMouseDown()
        {
            if (CompareAgainst == FactorData)
                CompareAgainst = null;
            else
                CompareAgainst = FactorData;
        }


        private string GetTooltipString()
        {
            var factors = new Dictionary<string, float>();
            foreach (var recordKVP in FactorData.FactorRecords)
                factors.Add(recordKVP.Key, GetEntryValue(recordKVP.Value, FactorData.MoveType));

            var value = FactorData.Value;
            if (CompareAgainst != null)
            {
                foreach (var recordKVP in CompareAgainst.FactorRecords)
                    factors[recordKVP.Key] -= GetEntryValue(recordKVP.Value, CompareAgainst.MoveType);

                value -= CompareAgainst.Value;
            }
            var tooltip = $"\t{value:0.00} [{Enum.GetName(typeof(MoveType), FactorData.MoveType)}]";

            if (CompareAgainst != null)
                tooltip += " COMPARISON";

            var sortedNames = factors.Keys.ToList();
            sortedNames.Sort((one, two) =>
                Mathf.Abs(factors[one]).CompareTo(Mathf.Abs(factors[two])));
            sortedNames.Reverse();

            foreach (var factorName in sortedNames)
            {
                if (Math.Abs(factors[factorName]) > 0.01)
                    tooltip += $"\n\t{factors[factorName]:0.00} : {factorName}";
            }

            return tooltip;
        }

        private static float GetEntryValue(EvaluationDebugLogRecord record, MoveType moveType)
        {
            return moveType == MoveType.Sprinting ? record.SprintValue : record.RegularValue;
        }
    }
}
