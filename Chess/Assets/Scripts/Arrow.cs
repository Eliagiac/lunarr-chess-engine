using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class Arrow : MonoBehaviour
{
    public static Arrow Instance;

    public float arrowWidth = 0.1f;
    public float arrowHeadSize = 0.1f;
    public Material arrowMaterial;

    List<GameObject> arrowObjects;
    int arrowIndex;


    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        arrowObjects = new();
        DrawArrow2D(Vector2.zero, new Vector2(2, 2), Color.yellow, zPos: -1);
    }

    /// Draw a 2D arrow (on xy plane)
    public static void DrawArrow2D(Vector2 start, Vector2 end, Color color, bool flatHead = true, float zPos = 0)
    {
        if (Instance.arrowIndex >= Instance.arrowObjects.Count)
        {
            GameObject arrowObject = new GameObject("Arrow");
            arrowObject.transform.parent = Instance.transform;

            var renderer = arrowObject.AddComponent<MeshRenderer>();
            var filter = arrowObject.AddComponent<MeshFilter>();
            renderer.material = Instance.arrowMaterial;
            filter.mesh = new Mesh();
            Instance.arrowObjects.Add(arrowObject);
        }

        Instance.arrowObjects[Instance.arrowIndex].transform.position = new Vector3(0, 0, zPos);
        Instance.arrowObjects[Instance.arrowIndex].SetActive(true);
        Instance.arrowObjects[Instance.arrowIndex].GetComponent<MeshRenderer>().material.color = color;
        Mesh mesh = Instance.arrowObjects[Instance.arrowIndex].GetComponent<MeshFilter>().mesh;
        CreateArrowMesh(ref mesh, start, end, Instance.arrowWidth, Instance.arrowHeadSize, flatHead);
        Instance.arrowIndex++;
    }

    private static void CreateArrowMesh(ref Mesh mesh, Vector2 start, Vector2 end, float lineWidth, float headSize, bool flatHead = true)
    {
        if (mesh == null)
        {
            mesh = new Mesh();
        }
        Vector2 forward = (end - start).normalized;
        Vector2 perp = Vector3.Cross(forward, Vector3.forward);

        Vector3[] verts = new Vector3[7];

        float actualHeadSize = lineWidth * 2 + headSize;
        float headBackAmount = (flatHead) ? 0 : 0.35f;
        end -= forward * actualHeadSize;
        verts[0] = start - perp * lineWidth / 2;
        verts[1] = start + perp * lineWidth / 2;
        verts[2] = end - perp * lineWidth / 2;
        verts[3] = end + perp * lineWidth / 2;
        verts[4] = end + forward * actualHeadSize;
        verts[5] = end - forward * actualHeadSize * headBackAmount - perp * actualHeadSize / 2;
        verts[6] = end - forward * actualHeadSize * headBackAmount + perp * actualHeadSize / 2;

        mesh.vertices = verts;
        mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3, 2, 5, 4, 2, 4, 3, 3, 4, 6 };
        mesh.RecalculateBounds();
    }

    public static void ClearArrows()
    {
        for (int i = 0; i < Instance.arrowObjects.Count; i++)
        {
            Destroy(Instance.arrowObjects[i]);
        }
    }
}