using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;
using static System.Math;

public class TreeParser : MonoBehaviour
{
    public static bool ShouldUpdate;

    public static string TreeString;

    public static Node Tree;

    private int TreeSize;

    private int MaxDepth;

    private float VerticalSize;
    // Indexed by ply.
    private float[] HorizontalSize; 
    private float[] Size;

    private int[] CurrentIndex;

    public Transform NodesParent;

    [SerializeField]
    private GameObject _leafNodePrefab;
    [SerializeField]
    private GameObject _pvNodePrefab;
    [SerializeField]
    private GameObject _allNodePrefab;
    [SerializeField]
    private GameObject _cutNodePrefab;
    [SerializeField]
    private GameObject _prunedNodePrefab;
    [SerializeField]
    private GameObject _transpositionTableCutoffNodePrefab;
    [SerializeField]
    private GameObject _linePrefab;

    [SerializeField]
    private Color _normalSearchColor;
    [SerializeField]
    private Color _lateMoveReductionsNormalSearchColor;
    [SerializeField]
    private Color _quiescenceSearchColor;
    [SerializeField]
    private Color _razoringQuiescenceSearchColor;
    [SerializeField]
    private Color _nullMovePruningNormalSearchColor;
    [SerializeField]
    private Color _probCutQuiescenceSearchColor;
    [SerializeField]
    private Color _probCutNormalSearchColor;
    [SerializeField]
    private Color _internalIterativeDeepeningNormalSearchColor;

    public static void UpdateTreeString(string s)
    {
        TreeString = s;
    }

    public void UpdateTreeData()
    {
        //string treeString = TreeString;
        //Tree = Node.Parse(ref treeString);

        MaxDepth = Tree.Length();
        TreeSize = Tree.Size();

        Debug.Log($"Length: {MaxDepth}");
        Debug.Log($"Size: {TreeSize}");

        VerticalSize = ((BoardVisualizer.Height) / 2) / MaxDepth;

        HorizontalSize = new float[MaxDepth];
        HorizontalSize[0] = BoardVisualizer.Width / 2;
        for (int i = 1; i < MaxDepth; i++) HorizontalSize[i] = ((BoardVisualizer.Width) / 2) / (Tree * i).Count;

        Size = new float[MaxDepth];
        for (int i = 0; i < MaxDepth; i++) Size[i] = Min(VerticalSize, HorizontalSize[i]);

        CurrentIndex = new int[MaxDepth];
    }

    public void LoadTreeDisplay()
    {
        SceneManager.LoadScene(1);
    }


    private void Update()
    {
        if (ShouldUpdate)
        {
            ShouldUpdate = false;

            UpdateTree();
        }
    }

    public void UpdateTree()
    {
        //TreeString = System.IO.File.ReadAllText(@"C:\RootNode.txt");

        //if (TreeString == null || TreeString == "") return;
        if (NodesParent == null) return;

        float time1 = Time.realtimeSinceStartup;
        UpdateTreeData();
        Debug.Log($"Time to update tree data: {Time.realtimeSinceStartup - time1}");

        if (Tree == null) return;

        while (NodesParent.transform.childCount > 0)
        {
            DestroyImmediate(NodesParent.transform.GetChild(0).gameObject);
        }

        float time2 = Time.realtimeSinceStartup;
        InstantiateTree(Tree, null);
        Debug.Log($"Updated successfully! {Time.realtimeSinceStartup - time2}");

        TreeString = "";


        void InstantiateTree(Node tree, Node parentNode, Transform parent = null)
        {
            GameObject root = Instantiate(
                tree.NodeType == NodeType.LeafNode ? _leafNodePrefab :
                tree.NodeType == NodeType.PVNode ? _pvNodePrefab :
                tree.NodeType == NodeType.CutNode ? _cutNodePrefab :
                tree.NodeType == NodeType.AllNode ? _allNodePrefab :
                tree.NodeType == NodeType.PrunedNode ? _prunedNodePrefab :
                tree.NodeType == NodeType.TranspositionTableCutoffNode ? _transpositionTableCutoffNodePrefab :
                null,
                NodesParent);

            root.transform.localScale = new(Size[tree.Ply], Size[tree.Ply]);

            var horizontalPos = GetHorizontalPosition(CurrentIndex[tree.Ply]++, tree.Ply);
            var verticalPos = GetVerticalPosition(tree.Ply);
            root.transform.position = new(horizontalPos - BoardVisualizer.Width / 2, BoardVisualizer.Height / 2 - verticalPos);


            Color color =
                tree.SearchType == SearchType.Normal ? _normalSearchColor :
                tree.SearchType == SearchType.LateMoveReductionsNormal ? _lateMoveReductionsNormalSearchColor :
                tree.SearchType == SearchType.Quiescence ? _quiescenceSearchColor :
                tree.SearchType == SearchType.RazoringQuiescence ? _razoringQuiescenceSearchColor :
                tree.SearchType == SearchType.NullMovePruningNormal ? _nullMovePruningNormalSearchColor :
                tree.SearchType == SearchType.ProbCutQuiescence ? _probCutQuiescenceSearchColor :
                tree.SearchType == SearchType.ProbCutNormal ? _probCutNormalSearchColor :
                tree.SearchType == SearchType.InternalIterativeDeepeningNormal ? _internalIterativeDeepeningNormalSearchColor :
                Color.black;

            root.GetComponent<SpriteRenderer>().color = color;

            if (parent != null) 
            {
                GameObject line = Instantiate(_linePrefab, NodesParent);

                line.GetComponent<LineRenderer>().SetPosition(0, new(root.transform.position.x, root.transform.position.y, -5));
                line.GetComponent<LineRenderer>().SetPosition(1, new(parent.position.x, parent.position.y, -5));

                line.GetComponent<LineRenderer>().widthMultiplier = (10f / TreeSize) + 0.01f;

                Color lineColor =
                    parentNode.SearchType == SearchType.Normal ? _normalSearchColor :
                    parentNode.SearchType == SearchType.LateMoveReductionsNormal ? _lateMoveReductionsNormalSearchColor :
                    parentNode.SearchType == SearchType.Quiescence ? _quiescenceSearchColor :
                    parentNode.SearchType == SearchType.RazoringQuiescence ? _razoringQuiescenceSearchColor :
                    parentNode.SearchType == SearchType.NullMovePruningNormal ? _nullMovePruningNormalSearchColor :
                    parentNode.SearchType == SearchType.ProbCutQuiescence ? _probCutQuiescenceSearchColor :
                    parentNode.SearchType == SearchType.ProbCutNormal ? _probCutNormalSearchColor :
                    parentNode.SearchType == SearchType.InternalIterativeDeepeningNormal ? _internalIterativeDeepeningNormalSearchColor :
                    Color.black;

                Gradient gradient = new();
                GradientColorKey[] colorKey = new GradientColorKey[2];
                GradientAlphaKey[] alphaKey = new GradientAlphaKey[2];

                colorKey[0].color = lineColor;
                colorKey[0].time = 0.0f;
                colorKey[1].color = lineColor;
                colorKey[1].time = 1.0f;

                alphaKey[0].alpha = 1.0f;
                alphaKey[0].time = 0.0f;
                alphaKey[1].alpha = 1.0f;
                alphaKey[1].time = 1.0f;

                gradient.SetKeys(colorKey, alphaKey);

                line.GetComponent<LineRenderer>().colorGradient = gradient;
            }

            foreach (var child in tree.Children) InstantiateTree(child, tree, root.transform);
        }

        float GetVerticalPosition(int ply)
        {
            return (VerticalSize * 2 * ply) + VerticalSize;
        }

        float GetHorizontalPosition(int index, int ply)
        {
            return (HorizontalSize[ply] * 2 * index) + HorizontalSize[ply];
        }
    }
}
