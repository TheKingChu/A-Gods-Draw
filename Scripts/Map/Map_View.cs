//CHARLIE SCRIPT
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Drawing;
using Color = UnityEngine.Color;

namespace Map
{
    public class Map_View : MonoBehaviour
    {
        public enum MapOrientations
        {
            BottomToTop,
            TopToBottom,
            RightToLeft,
            LeftToRight,
            ForwardToBack,
            BackToForward,
            None
        }

        public Map_Manager mapManager;
        public MapOrientations orientations;

        public List<Map_Configuration> allMapConfigs;
        public GameObject nodePrefab;
        public float orientationOffset;
        public Vector2 ScrollBounds;

        [Header("Background Settings")]
        [Tooltip("If the background sprite is null, background will not be shown")]
        public Sprite background;
        public Color bgColor = Color.white;
        public float xSize;
        public float yOffset;

        [Header("Line Settings")]
        public GameObject linePrefab;

        [Range(3, 10)]
        public int linePointCount = 10;
        public float offsetFromNodes = 0.5f;

        [Header("colors")]
        public Color32 visitedColor = Color.white;
        public Color32 lockedColor = Color.gray;
        public Color32 lineVisitedColor = Color.white;
        public Color32 lineLockedColor = Color.gray;
        public Color32 AvailableColor = Color.blue;

        private GameObject firstParent;
        private GameObject mapParent;
        private List<List<MapPoint>> paths;
        private Camera cam;

        public readonly List<Map_Nodes> MapNodes = new List<Map_Nodes>();
        private readonly List<Path> path = new List<Path>(); //the path is the same as a line connection!

        public static Map_View instance;

        private void Awake()
        {
            instance = this;
            cam = Camera.main;
        }

        private void ClearMap()
        {
            if (firstParent != null) 
            {
                Destroy(firstParent);
            }

            MapNodes.Clear();
            path.Clear();
        }

        public void MapShow(Map m)
        {
            if (m == null)
            {
                Debug.LogWarning("Map was null in MapView.MapShow()");
                return;
            }

            ClearMap();
            CreateParent();
            CreateNodes(m.nodes);
            DrawPath();
            Orientation();
            ResetRotation();
            SetPickableNodes();
            SetPathColor();
            CreateBackground(m);
        }

        private void CreateBackground(Map m)
        {
            if (background == null)
            {
                return;
            }

            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(mapParent.transform, false);

            var bossNode = MapNodes.FirstOrDefault(node => node.Node.nodeType == NodeType.Boss);
            var span = m.DistLayers(); //distance between first and last layers

            backgroundObject.transform.localPosition = new Vector3(bossNode.transform.localPosition.x, span / 2f, -2f);
            backgroundObject.transform.localRotation = Quaternion.identity;

            var spriteRenderer = backgroundObject.AddComponent<SpriteRenderer>();
            spriteRenderer.color = bgColor;
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.sprite = background;
            spriteRenderer.size = new Vector2(xSize, span + yOffset * 2f);
            spriteRenderer.transform.position = new Vector3(0f, 5f, 2f);
        }

        private void CreateParent()
        {
            firstParent = new GameObject("OuterPartParent");
            firstParent.transform.SetParent(gameObject.transform.parent, false);
            mapParent = new GameObject("MapParentScrolling");
            mapParent.transform.SetParent(firstParent.transform, false);

            var scrollNonUI = mapParent.AddComponent<ScrollNonUI>();

            if(orientations == MapOrientations.BottomToTop || orientations == MapOrientations.TopToBottom)
            {
                scrollNonUI.freezeX = true;
                scrollNonUI.freezeY = false;
                scrollNonUI.freezeZ = true;
            }
            if(orientations == MapOrientations.RightToLeft || orientations == MapOrientations.LeftToRight)
            {
                scrollNonUI.freezeX = false;
                scrollNonUI.freezeY = true;
                scrollNonUI.freezeZ = true;
            }
            if(orientations == MapOrientations.ForwardToBack || orientations == MapOrientations.BackToForward)
            {
                scrollNonUI.freezeX = true;
                scrollNonUI.freezeY = true;
                scrollNonUI.freezeZ = false;
            }

            // var boxColl = mapParent.AddComponent<BoxCollider>();
            // boxColl.size = new Vector3(100, 100, 1); //can be changed

        }

        private void CreateNodes(IEnumerable<Node> nodes)
        {
            foreach (var node in nodes)
            {
                var mapNode = CreateMapNode(node);
                MapNodes.Add(mapNode);
            }
        }

        private Map_Nodes CreateMapNode(Node node)
        {
            var mapNodeObject = Instantiate(nodePrefab);
            mapNodeObject.transform.SetParent(mapParent.transform, false);
            var mapNode = mapNodeObject.GetComponent<Map_Nodes>();
            var blueprint = GetNodeBlueprint(node.blueprintName);

            mapNode.SetUp(node, blueprint);
            mapNode.transform.localPosition = node.pos;

            return mapNode;
        }


        public void SetPickableNodes() //fix
        {
            //here we are putting all the map nodes as locked/non pickable
            foreach (var node in MapNodes)
            {
                node.SetState(NodeStates.Locked);
            }

            if (mapManager.CurrentMap.path.Count == 0)
            {
                foreach (var node in MapNodes.Where(n => n.Node.point.y == 0))
                {
                    node.SetState(NodeStates.Taken);
                }
            }
            else
            {
                foreach (var point in mapManager.CurrentMap.path)
                {
                    var mapNodes = GetNodes(point);
                    if (mapNodes != null)
                    {
                        mapNodes.SetState(NodeStates.Visited);
                    }
                }

                var currentPoint = mapManager.CurrentMap.path[mapManager.CurrentMap.path.Count - 1];
                var currentNode = mapManager.CurrentMap.GetNode(currentPoint);

                foreach(var point in currentNode.outgoing)
                {
                    var mapNode = GetNodes(point);
                    if(mapNode != null)
                    {
                        mapNode.SetState(NodeStates.Taken);
                    }
                }
            }
        }
        public void SetPathColor()
        {
            foreach(var connection in path) //path is line connections
            {
                connection.SetColor(lineLockedColor);
            }

            if(mapManager.CurrentMap.path.Count == 0)
            {
                return;
            }

            var currentPoint = mapManager.CurrentMap.path[mapManager.CurrentMap.path.Count - 1];
            var currentNode = mapManager.CurrentMap.GetNode(currentPoint);

            foreach(var point in currentNode.outgoing)
            {
                var pathConnection = path.FirstOrDefault(conn => conn.from.Node == currentNode && conn.to.Node.point.Equals(point));
                pathConnection?.SetColor(lineVisitedColor);
            }

            if(mapManager.CurrentMap.path.Count <= 1)
            {
                return;
            }

            for(var i = 0; i < mapManager.CurrentMap.path.Count - 1; i++)
            {
                var current = mapManager.CurrentMap.path[i];
                var next = mapManager.CurrentMap.path[i + 1];
                var pathConnection = path.FirstOrDefault(conn => conn.from.Node.point.Equals(current) && conn.to.Node.point.Equals(next));
                pathConnection?.SetColor(lineVisitedColor);
            }
        }

        private void Orientation()
        {
            var scrollNonUI = mapParent.GetComponent<ScrollNonUI>();
            var span = mapManager.CurrentMap.DistLayers();
            var bossNode = MapNodes.FirstOrDefault(node => node.Node.nodeType == NodeType.Boss);
            scrollNonUI.ScrollMinMaxBounds = ScrollBounds;

            // firstParent.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
            var offset = orientationOffset;
            Vector3 desiredPos;


            switch (orientations)
            {
                case MapOrientations.BottomToTop:
                    if(scrollNonUI != null)
                    {
                        scrollNonUI.yConst.max = 0;
                        scrollNonUI.yConst.min = -(span + 2f * offset);
                    }
                    desiredPos = firstParent.transform.localPosition + new Vector3(0, offset, 0);
                    float y = Mathf.Clamp(desiredPos.y, ScrollBounds.x, ScrollBounds.y); 
                    desiredPos.y = y;
                    firstParent.transform.localPosition = desiredPos;
                    break;

                case MapOrientations.TopToBottom:
                    mapParent.transform.eulerAngles = new Vector3(0, 0, 180);
                    if(scrollNonUI != null)
                    {
                        scrollNonUI.yConst.min = 0;
                        scrollNonUI.yConst.max = span + 2f * offset;
                    }
                    desiredPos = firstParent.transform.localPosition + new Vector3(0, -offset, 0);
                    float _y = Mathf.Clamp(desiredPos.y, ScrollBounds.x, ScrollBounds.y); 
                    desiredPos.y = _y;
                    firstParent.transform.localPosition = desiredPos;
                    break;

                case MapOrientations.RightToLeft:
                    offset *= cam.aspect;
                    mapParent.transform.eulerAngles = new Vector3(0, 0, 90);
                    desiredPos = firstParent.transform.localPosition + new Vector3(offset, bossNode.transform.position.y, 0);
                    float x = Mathf.Clamp(desiredPos.x, ScrollBounds.x, ScrollBounds.y); 
                    desiredPos.x = x;
                    firstParent.transform.localPosition = desiredPos;
                    if(scrollNonUI != null)
                    {
                        scrollNonUI.xConst.max = span + 2f * offset;
                        scrollNonUI.xConst.min = 0;
                    }
                    break;

                case MapOrientations.LeftToRight:
                    offset *= cam.aspect;
                    mapParent.transform.eulerAngles = new Vector3(0, 0, -90);
                    desiredPos = firstParent.transform.localPosition + new Vector3(-offset, bossNode.transform.position.y, 0);
                    float _x = Mathf.Clamp(desiredPos.x, ScrollBounds.x, ScrollBounds.y); 
                    desiredPos.x = _x;
                    firstParent.transform.localPosition = desiredPos;
                    if(scrollNonUI != null)
                    {
                        scrollNonUI.xConst.max = 0;
                        scrollNonUI.xConst.min = -(span + 2 * offset);
                    }
                    break;

                case MapOrientations.ForwardToBack:
                    offset *= cam.aspect;
                    mapParent.transform.eulerAngles = new Vector3(90, 0, 0);
                    desiredPos = firstParent.transform.localPosition + new Vector3(0, 0, offset);
                    float z = Mathf.Clamp(desiredPos.z, ScrollBounds.x, ScrollBounds.y); 
                    desiredPos.z = z;
                    firstParent.transform.localPosition = desiredPos;
                    if(scrollNonUI != null)
                    {
                        scrollNonUI.zConst.min = span + 2 * offset;
                        scrollNonUI.zConst.max = 0;
                    }
                    break;

                case MapOrientations.BackToForward:
                    offset *= cam.aspect;
                    mapParent.transform.eulerAngles = new Vector3(-90, 0, 0);
                    desiredPos = firstParent.transform.localPosition + new Vector3(0, 0, -offset);
                    float _z = Mathf.Clamp(desiredPos.z, ScrollBounds.x, ScrollBounds.y); 
                    desiredPos.z = _z;
                    firstParent.transform.localPosition = desiredPos;
                    if(scrollNonUI != null)
                    {
                        scrollNonUI.zConst.max = 0;
                        scrollNonUI.zConst.min = -(span + 2 * offset);
                    }
                    break;

                case MapOrientations.None:
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void DrawPath()
        {
            foreach(var node in MapNodes)
            {
                foreach(var connection in node.Node.outgoing)
                {
                    AddPathConnection(node, GetNodes(connection));
                }
            }
        }

        private void ResetRotation() //node rot
        {
            foreach(var node in MapNodes)
            {
                node.transform.rotation = Quaternion.identity;
            }
        }

        public void AddPathConnection(Map_Nodes from, Map_Nodes to)
        {
            var pathObject = Instantiate(linePrefab);
            pathObject.transform.SetParent(mapParent.transform, false);
            // var lineRenderer = pathObject.GetComponent<LineRenderer>();
            var fromPoint = from.transform.position + (to.transform.position - from.transform.position).normalized * offsetFromNodes;
            var toPoint = to.transform.position + (from.transform.position - to.transform.position).normalized * offsetFromNodes;

            // trying to replace with models
            pathObject.transform.position =  Vector3.Lerp(fromPoint, toPoint, 0.5f);
            pathObject.transform.LookAt(fromPoint);
            Vector3 scale = pathObject.transform.localScale;
            float dist = Vector3.Distance(from.transform.position, to.transform.position);
            pathObject.transform.localScale = new Vector3(scale.x, scale.y, scale.z + dist*100);

            // pathObject.transform.position =  fromPoint;
            // lineRenderer.useWorldSpace = false;
            // lineRenderer.positionCount = linePointCount;

            for(var i = 0; i < linePointCount; i++)
            {
                // lineRenderer.SetPosition(i, Vector3.Lerp(Vector3.zero, toPoint - fromPoint, (float)i / (linePointCount - 1)));
            }

            var dottetLine = pathObject.GetComponent<DottetPath>();

            if(dottetLine != null)
            {
                dottetLine.ScaleMat();
            }

            // path.Add(new Path(lineRenderer, from, to));
        }

        private Map_Nodes GetNodes(MapPoint p)
        {
            return MapNodes.FirstOrDefault(n => n.Node.point.Equals(p));
        }

        private Map_Configuration GetConfiguration(string configName)
        {
            return allMapConfigs.FirstOrDefault(c => c.name == configName);
        }

        public NodeBlueprint GetNodeBlueprint(NodeType type)
        {
            var config = GetConfiguration(mapManager.CurrentMap.configName);
            return config.nodeBlueprints.FirstOrDefault(n => n.nodeType == type);
        }

        public NodeBlueprint GetNodeBlueprint(string blueprintName)
        {
            var config = GetConfiguration(mapManager.CurrentMap.configName);
            return config.nodeBlueprints.FirstOrDefault(n => n.name == blueprintName);
        }
    }
}