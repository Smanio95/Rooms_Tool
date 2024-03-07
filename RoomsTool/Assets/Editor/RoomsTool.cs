using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class Toggable
{
    public bool isToggled = false;
}

public class PrefabsBlock : Toggable
{
    public string blockName;
    public List<RoomsBlock> roomBlocks;

    public PrefabsBlock(string _blockName, List<RoomsBlock> _roomBlocks)
    {
        blockName = _blockName;
        roomBlocks = _roomBlocks;
    }
}

public class RoomsBlock : Toggable
{
    public string roomName;
    public List<RoomInfo> prefabs;

    public RoomsBlock(string _roomName, List<RoomInfo> _prefabs)
    {
        roomName = _roomName;
        prefabs = _prefabs;
    }
}

public class RoomInfo : Toggable
{
    public Room prefab;

    public RoomInfo(Room _prefab)
    {
        prefab = _prefab;
    }
}

public class RoomsTool : EditorWindow
{
    [MenuItem(Constants.TOOL_NAME)]
    public static void OpenRoomsTool() => GetWindow<RoomsTool>();

    public LayerMask layerMask;
    public float snappingRadius = 2;
    public float snappingHardness = 3;

    SerializedObject so;
    SerializedProperty snappingRadiusP;
    SerializedProperty layerMaskP;
    SerializedProperty snappingHardnessP;

    private int currentBlockIndex = -1;
    private int currentRoomIndex = -1;
    private int currentPrefabIndex = -1;
    private List<PrefabsBlock> prefabBlocks = new();

    private RoomInfo selectedPrefab;

    private int currentSelectionIndex = 0;
    private Texture2D currentSelectionArrow;

    private Quaternion defaultRotation = Quaternion.identity;
    private Vector3 rectAngle = new(0, 90, 0);

    private Vector3 snapPoint;

    private bool hasFoundSnapping;

    private void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGUI;

        so = new(this);
        layerMaskP = so.FindProperty("layerMask");
        snappingRadiusP = so.FindProperty("snappingRadius");
        snappingHardnessP = so.FindProperty("snappingHardness");

        string[] guids = AssetDatabase.FindAssets("t:folder", new[] { Constants.FOLDER_PATH });
        IEnumerable<string> paths = guids.Select(AssetDatabase.GUIDToAssetPath);

        OrderPrefabs(paths);

        guids = AssetDatabase.FindAssets("t:texture2D", new[] { Constants.TEXTURE_PATH });
        paths = guids.Select(AssetDatabase.GUIDToAssetPath);
        Texture2D[] textures = paths.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).ToArray();

        if (textures.Length > 0) currentSelectionArrow = textures[0];
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    private void OnGUI()
    {
        so.Update();
        EditorGUILayout.PropertyField(layerMaskP);
        EditorGUILayout.PropertyField(snappingRadiusP);
        EditorGUILayout.PropertyField(snappingHardnessP);

        snappingRadiusP.floatValue = Mathf.Max(snappingRadiusP.floatValue, 0);
        snappingHardnessP.floatValue = Mathf.Max(snappingHardnessP.floatValue, snappingRadiusP.floatValue);

        EditorGUILayout.LabelField(Constants.EXPLANATION);
        EditorStyles.label.wordWrap = true;

        if (so.ApplyModifiedProperties())
        {
            SceneView.RepaintAll();
        }
    }

    private void DuringSceneGUI(SceneView view)
    {
        Handles.BeginGUI();

        HandleSelection();

        CreateViewGUI();

        Handles.EndGUI();

        if (Event.current.type == EventType.MouseMove)
        {
            view.Repaint();
        }

        if (Event.current.shift && Event.current.type == EventType.ScrollWheel)
        {
            float deltaMultiplier = Event.current.delta.x > 0 ? -1 : 1;

            defaultRotation = Quaternion.Euler(rectAngle * deltaMultiplier + defaultRotation.eulerAngles);

            view.Repaint();
        }

        #region Spawn Prefab
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

        if (selectedPrefab != null && (Physics.Raycast(ray, out RaycastHit hit, 1000, layerMask) || hasFoundSnapping))
        {
            Vector3 point = hit.point;
            point.y = 0;

            if (hasFoundSnapping)
            {
                if (Vector3.SqrMagnitude(snapPoint - hit.point) <= Mathf.Pow(snappingHardness, 2))
                {
                    point = snapPoint;
                }
                else
                {
                    hasFoundSnapping = false;
                }
            }

            Pose currentPose = new(point, defaultRotation);

            DrawPreview(currentPose);

            Handles.color = Color.blue;
            Handles.DrawWireDisc(point, Vector3.up, snappingHardness, 3);

            Vector2 lengths = RetrieveHalfLengths(true);
            Handles.color = Color.green;

            if (!CheckNeighbors(currentPose.position, lengths))
            {
                if (Event.current.modifiers == EventModifiers.None
                    && Event.current.type == EventType.KeyUp
                    && Event.current.keyCode == KeyCode.Space)
                {
                    SpawnPrefab(currentPose);
                }
            }
            else
            {
                Handles.color = Color.red;
            }

            Handles.DrawWireCube(point, new(lengths.x * 2, 0.1f, lengths.y * 2));
        }
        #endregion
    }

    private void HandleSelection()
    {
        if (Event.current.shift && Event.current.type == EventType.KeyUp)
        {
            switch (Event.current.keyCode)
            {
                case KeyCode.W:
                    currentSelectionIndex = Mathf.Max(currentSelectionIndex - 1, 0);
                    break;
                case KeyCode.S:
                    currentSelectionIndex = Mathf.Min(currentSelectionIndex + 1, 2);
                    break;
                case KeyCode.A:
                    HandleHorizontalShift(-1);
                    break;
                case KeyCode.D:
                    HandleHorizontalShift(1);
                    break;
            }
        }
    }

    private void HandleHorizontalShift(int dir)
    {
        switch (currentSelectionIndex)
        {
            case 0:
                HorizontalChange(prefabBlocks.Select(r => (Toggable)r).ToList(), dir, ref currentBlockIndex);
                break;
            case 1:
                HorizontalChange(prefabBlocks[currentBlockIndex].roomBlocks.Select(r => (Toggable)r).ToList(), dir, ref currentRoomIndex);
                break;
            case 2:
                PrefabHorizontalChange(prefabBlocks[currentBlockIndex].roomBlocks[currentRoomIndex].prefabs, dir);
                break;
            default:
                HorizontalChange(prefabBlocks.Select(r => (Toggable)r).ToList(), dir, ref currentBlockIndex);
                break;
        };
    }

    private void HorizontalChange(List<Toggable> lst, int dir, ref int index)
    {
        //currentPrefabIndex = prefabIndex;

        dir = Mathf.Clamp(dir + index, 0, lst.Count - 1);

        index = dir;

        for (int i = 0; i < lst.Count; i++)
        {
            lst[i].isToggled = i == dir;
        }
    }

    private void PrefabHorizontalChange(List<RoomInfo> lst, int dir)
    {
        if (prefabBlocks[currentBlockIndex].roomBlocks[currentRoomIndex].prefabs.Any(r => r.isToggled == true))
        {
            dir = Mathf.Clamp(dir + currentPrefabIndex, 0, lst.Count - 1);
        }
        else
        {
            dir = 0;
        }

        currentPrefabIndex = dir;

        for (int i = 0; i < lst.Count; i++)
        {
            lst[i].isToggled = i == dir;
        }

        if (selectedPrefab != null && lst[currentPrefabIndex].prefab.GetInstanceID() != selectedPrefab.prefab.GetInstanceID())
        {
            selectedPrefab.isToggled = false;
        }

        selectedPrefab = lst[currentPrefabIndex];

    }

    private void OrderPrefabs(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            // look for name
            string name = path.Split("/")[^1];

            // retrieve prefabs
            string[] guids = AssetDatabase.FindAssets("t:prefab", new[] { path });
            IEnumerable<Room> specificPaths = guids.Select(s =>
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(s);
                return AssetDatabase.LoadAssetAtPath<Room>(assetPath);
            });

            // build block
            prefabBlocks.Add(new(name, CreateRoomsBlocks(specificPaths.ToArray())));
        }

        // valorize first block
        if (prefabBlocks.Count > 0)
        {
            prefabBlocks[0].isToggled = true;
            currentBlockIndex = 0;

            if (prefabBlocks[0].roomBlocks.Count > 0)
            {
                prefabBlocks[0].roomBlocks[0].isToggled = true;
                currentRoomIndex = 0;
            }
        }
    }

    private List<RoomsBlock> CreateRoomsBlocks(Room[] rooms)
    {
        if (rooms.Length == 0) return new();

        List<RoomsBlock> lst = new();

        Room[] ordered = rooms.OrderBy(room => room.NumOfEntraces).ToArray();

        int nOfEntrances = ordered[0].NumOfEntraces;

        List<RoomInfo> roomLst = new();

        int currentIndex = 0;

        while (currentIndex < ordered.Length)
        {
            if (nOfEntrances < ordered[currentIndex].NumOfEntraces)
            {
                lst.Add(new(Constants.BASE_ORDER_STRING + nOfEntrances, roomLst));

                while (nOfEntrances < ordered[currentIndex].NumOfEntraces)
                {
                    nOfEntrances++;
                }

                roomLst = new();
            }

            roomLst.Add(new(ordered[currentIndex]));

            currentIndex++;
        }

        if (roomLst.Count > 0) lst.Add(new(Constants.BASE_ORDER_STRING + nOfEntrances, roomLst));

        return lst;
    }

    private void BuildSelector(int index)
    {
        float posY = index switch
        {
            0 => 0,
            1 => 1,
            2 => 2.3f,
            _ => 0
        };

        Rect textureRect = new Rect(25, 25 + 55 * posY, 25, 25);
        GUI.DrawTexture(textureRect, currentSelectionArrow);
    }

    private void CreateViewGUI()
    {
        BuildSelector(currentSelectionIndex);

        Rect rect = new Rect(70, 10, 150, 50);

        for (int i = 0; i < prefabBlocks.Count; i++)
        {
            EditorGUI.BeginChangeCheck();

            bool value = GUI.Toggle(rect, prefabBlocks[i].isToggled, prefabBlocks[i].blockName);

            if (i != currentBlockIndex && value != prefabBlocks[i].isToggled)
            {
                prefabBlocks[i].isToggled = value;
            }

            if (EditorGUI.EndChangeCheck())
            {
                currentSelectionIndex = 0;

                if (prefabBlocks[i].isToggled && currentBlockIndex != i)
                {
                    if (currentBlockIndex >= 0)
                    {
                        prefabBlocks[currentBlockIndex].isToggled = false;
                    }

                    currentBlockIndex = i;

                    if (prefabBlocks[currentBlockIndex].roomBlocks.Count > 0)
                    {
                        currentRoomIndex = Mathf.Max(prefabBlocks[currentBlockIndex].roomBlocks.FindIndex(r => r.isToggled), 0);
                        prefabBlocks[currentBlockIndex].roomBlocks[currentRoomIndex].isToggled = true;
                    }
                }

            }

            rect.x += rect.width + 2;
        }

        if (currentBlockIndex >= 0)
        {
            CreateSubFolderView(prefabBlocks[currentBlockIndex].roomBlocks);
        }
    }

    private void CreateSubFolderView(List<RoomsBlock> roomBlocks)
    {
        Rect rect = new Rect(70, 70, 150, 50);

        for (int i = 0; i < roomBlocks.Count; i++)
        {
            EditorGUI.BeginChangeCheck();

            bool value = GUI.Toggle(rect, roomBlocks[i].isToggled, roomBlocks[i].roomName);

            if (i != currentRoomIndex && value != roomBlocks[i].isToggled)
            {
                roomBlocks[i].isToggled = value;
            }

            if (EditorGUI.EndChangeCheck())
            {
                currentSelectionIndex = 1;

                if (roomBlocks[i].isToggled && currentRoomIndex != i)
                {
                    if (currentRoomIndex >= 0) roomBlocks[currentRoomIndex].isToggled = false;

                    currentRoomIndex = i;

                    if (prefabBlocks[currentBlockIndex].roomBlocks[currentRoomIndex].prefabs.Count > 0)
                    {
                        currentPrefabIndex = Mathf.Max(prefabBlocks[currentBlockIndex].roomBlocks[currentRoomIndex].prefabs.FindIndex(r => r.isToggled), 0);
                        prefabBlocks[currentBlockIndex].roomBlocks[currentRoomIndex].isToggled = true;
                    }
                }
            }

            rect.x += rect.width + 2;
        }

        if (currentRoomIndex >= 0)
        {
            CreatePrefabView(prefabBlocks[currentBlockIndex].roomBlocks[currentRoomIndex].prefabs);
        }

    }

    private void CreatePrefabView(List<RoomInfo> rooms)
    {
        Rect rect = new Rect(70, 140, 150, 50);

        for (int i = 0; i < rooms.Count; i++)
        {
            Texture icon = AssetPreview.GetAssetPreview(rooms[i].prefab.gameObject);

            EditorGUI.BeginChangeCheck();

            bool value = GUI.Toggle(rect, rooms[i].isToggled, new GUIContent(rooms[i].prefab.name, icon));

            if ((selectedPrefab == null || rooms[i].prefab.GetInstanceID() != selectedPrefab.prefab.GetInstanceID()) && value != rooms[i].isToggled)
            {
                rooms[i].isToggled = value;
            }

            if (EditorGUI.EndChangeCheck())
            {
                currentSelectionIndex = 2;

                if (rooms[i].isToggled && (selectedPrefab == null || rooms[i].prefab.GetInstanceID() != selectedPrefab.prefab.GetInstanceID()))
                {
                    currentPrefabIndex = i;

                    if (currentPrefabIndex >= 0 && selectedPrefab != null)
                    {
                        selectedPrefab.isToggled = false;
                    }

                    selectedPrefab = rooms[i];
                }
            }

            rect.x += rect.width + 2;
        }
    }

    private bool CheckNeighbors(Vector3 point, Vector2 lengths)
    {
        if (selectedPrefab == null) return true;

        point.y += 1;

        Collider[] colliders = Physics.OverlapBox(point, new(lengths.x - Constants.ACCEPTANCE, 0.1f, lengths.y - Constants.ACCEPTANCE));

        return colliders.Length > 0;
    }

    private Vector2 RetrieveHalfLengths(bool oriented = false)
    {
        if (oriented)
        {
            bool isStraight = Mathf.RoundToInt(Vector3.Dot(Vector3.right, defaultRotation * Vector3.forward)) == 0;
            return new Vector2(isStraight ? selectedPrefab.prefab.width : selectedPrefab.prefab.depth,
                        isStraight ? selectedPrefab.prefab.depth : selectedPrefab.prefab.width) / 2;
        }

        return new Vector2(selectedPrefab.prefab.width, selectedPrefab.prefab.depth) / 2;
    }

    private Vector3 CreateSnapPoint(Vector3 offset, Ray snappingRay, bool offsetZModifier)
    {
        float dotValue = Mathf.RoundToInt(Vector3.Dot(snappingRay.direction, defaultRotation * Vector3.forward));
        bool arePerpendicular = dotValue == 0;

        Vector2 lengths = RetrieveHalfLengths();

        Vector3 snapPoint = snappingRay.origin + snappingRay.direction * (arePerpendicular ? lengths.x : lengths.y);

        float modifier;
        if (offsetZModifier)
        {
            modifier = offset.z;
        }
        else
        {
            modifier = offset.x;
        }

        float mult = RetrieveMultiplier();
        modifier *= mult;

        if (Mathf.RoundToInt(Vector3.Dot(snappingRay.direction, Vector3.forward)) == 0)
        {
            snapPoint.z -= modifier;
        }
        else
        {
            snapPoint.x += modifier;
        }

        return snapPoint;
    }

    private float RetrieveMultiplier()
    {
        int angle = (int)defaultRotation.eulerAngles.y % 360;
        return angle switch
        {
            0 => -1,
            90 => -1,
            180 => 1,
            270 => 1,
            _ => -1
        };
    }

    private void DrawPreview(Pose pose)
    {
        if (selectedPrefab == null) return;

        Matrix4x4 poseToWorldMtx = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);

        MeshFilter[] filters = selectedPrefab.prefab.GetComponentsInChildren<MeshFilter>();

        foreach (MeshFilter f in filters)
        {
            Matrix4x4 childToPose = f.transform.localToWorldMatrix;
            Matrix4x4 childToWorldMatrix = poseToWorldMtx * childToPose;

            Mesh mesh = f.sharedMesh;
            Material m = f.GetComponent<MeshRenderer>().sharedMaterial;
            m.SetPass(0);
            Graphics.DrawMeshNow(mesh, childToWorldMatrix);
        }

        foreach (Transform t in selectedPrefab.prefab.entrances)
        {
            Matrix4x4 childToPose = t.localToWorldMatrix;
            Matrix4x4 childToWorldMatrix = poseToWorldMtx * childToPose;
            Vector3 finalPos = childToWorldMatrix.GetPosition();

            Vector3 dir = pose.rotation * t.forward;

            if (!hasFoundSnapping
                && CheckDistance(finalPos, out Ray hitRay)
                && Mathf.RoundToInt(Vector3.Dot(dir, hitRay.direction)) < -0.99f)
            {
                hasFoundSnapping = true;
                snapPoint = CreateSnapPoint(t.localPosition - selectedPrefab.prefab.transform.position, hitRay, Mathf.RoundToInt(Vector3.Dot(dir, pose.rotation * Vector3.forward)) == 0);
                break;
            }
        }
    }

    private void SpawnPrefab(Pose pose)
    {
        Transform toSpawn = (Transform)PrefabUtility.InstantiatePrefab(selectedPrefab.prefab.transform);
        toSpawn.SetPositionAndRotation(pose.position, pose.rotation);
        Undo.RegisterCreatedObjectUndo(toSpawn.gameObject, "Spawn prefab");
    }

    private bool CheckDistance(Vector3 pos, out Ray hitRay)
    {
        hitRay = new();

        Collider[] colliders = Physics.OverlapSphere(pos, snappingRadius);

        Handles.color = Color.yellow;
        Handles.DrawWireDisc(pos, Vector3.up, snappingRadius, 3);

        if (colliders.Length > 0)
        {
            foreach (Collider hit in colliders)
            {
                if (hit.CompareTag("Door"))
                {
                    hitRay = new(hit.transform.parent.position, hit.transform.forward);

                    return true;
                }
            }
        }

        return false;
    }
}
