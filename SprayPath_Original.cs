using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino;
using Rhino.Geometry.Collections;
using static PrecisionNode.Utilities;

namespace PrecisionNode
{
    public class SprayPath : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public SprayPath()
          : base("Spray Path", "SP",
              "Compute the spray path of a node",
              "PrecisionNode", "Node Designer")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Nodes", "N", "The nodes to compute spray pattern for", GH_ParamAccess.list);
            pManager.AddNumberParameter("Division Length",
                "DA",
                "The distance between paths at the ends of each NodeBranch",
                GH_ParamAccess.item);
            pManager.AddIntegerParameter("Path Adhesiveness", "PA",
                "How adhesive the path is to the surface to be sprayed on",
                GH_ParamAccess.item);
            pManager.AddIntegerParameter("Mode", "M", "M=1: the path stems from a smooth SubD surface, M=2: the path stems from the control polygon of a SubD surface",
                GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Processed Node", "PN", "The Node objects after being processed", GH_ParamAccess.list);
            pManager.AddCurveParameter("Spray Path", "SP", "The spray path for the nodes", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Node> nodes = new List<Node>();
            double divisionLength = double.NaN;
            int pathadhesiveness = 0;
            int mode = 1;
            DA.GetDataList(0, nodes);
            DA.GetData(1, ref divisionLength);
            DA.GetData(2, ref pathadhesiveness);
            DA.GetData(3, ref mode);

            GH_Structure<GH_Curve> sprayPathTree = new GH_Structure<GH_Curve>();
            List<Node> processedNodes = new List<Node>();

            int branchIndex = 0;
            foreach(Node node in nodes)
            {
                if (node.CoreGeometry == null) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "This node doesn't have a core geometry yet");
                else
                {
                    //compute and store the sprayPath into the Node objects
                    List<Curve> sprayPath = ComputeSprayPath(node, divisionLength, pathadhesiveness, mode);
                    node.SprayPath = sprayPath;
                    processedNodes.Add(node);

                    //put the sprayPath into a data tree as Curves
                    GH_Path branchPath = new GH_Path(branchIndex);
                    foreach (Curve curve in sprayPath)
                    {
                        GH_Curve gHCurve = new GH_Curve(curve);
                        sprayPathTree.Append(gHCurve, branchPath);
                    }
                }
            }

            DA.SetDataList(0, processedNodes);
            DA.SetDataTree(1, sprayPathTree);

            


        }

        /// <summary>
        /// Compute the spray path
        /// </summary>
        /// <param name="node"></param>
        /// <param name="divisionLength"></param>
        /// <param name="pathAdhesiveness"></param>
        /// <param name="mode"></param>
        /// <param name="sub"></param>
        /// <param name="crvs"></param>
        /// <returns></returns>
        public static List<Curve> ComputeSprayPath(Node node, double divisionLength, int pathAdhesiveness, int mode)
        {
            List<Curve> paths = new List<Curve>();
            SubD SprayBaseSubD = node.SprayBaseSubD;

            //First loop finds all the intersection curves and examine if the intersection is closed,
            List<Point3d> loseEnds = new List<Point3d>();
            foreach (NodeBranch nodeBranch in node.NodeBranches)
            {
                Curve intersection = nodeBranch.CylinderIntersection;
                if (!intersection.IsClosed)
                {
                    loseEnds.Add(intersection.PointAtStart);
                    loseEnds.Add(intersection.PointAtEnd);
                }
            }

            //compute the average position of all the lose ends
            Point3d sum = new Point3d(0, 0, 0);
            foreach (Point3d point in loseEnds)
            {
                sum += point;
            }

            //second loop construct the node geometry of with dense iso curves
            Point3d averagePosition = sum / loseEnds.Count;
            List<SubD> branchSpraySubDs = new List<SubD>();
            foreach (NodeBranch nodeBranch in node.NodeBranches)
            {
                Brep brep;
                SubD subD;
                List<Point3d> intersectionCorners = Utilities.GetIntersectionCorners(nodeBranch.CylinderIntersection,
                  divisionLength,
                  nodeBranch.BranchStartPlane,
                  new List<Point3d> { averagePosition });

                intersectionCorners = Utilities.PlaneRadialPointSort(intersectionCorners, nodeBranch.BranchStartPlane);
                Utilities.LoftBranch(intersectionCorners,
                  nodeBranch.Radius + node.CoreThreadWallThickness,
                  nodeBranch.BranchStartPlane,
                  out brep,
                  out subD);
                branchSpraySubDs.Add(subD);
            }
            SubD sprayPathSubD = SubD.JoinSubDs(branchSpraySubDs, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, false)[0];
            

            //fail safe
            if (mode != 1 && mode != 2) { mode = 0; }

            List<Curve> InteriorEdges = new List<Curve>();
            //If the path stems from a smooth SubD surface
            if (mode == 1)
            {
                //get all the interior edges
                foreach (SubDEdge e in sprayPathSubD.Edges)
                {
                    if (e.FaceCount > 1) InteriorEdges.Add(e.ToNurbsCurve(true));
                }
            }
            else if (mode == 2)
            {
                Mesh sprayFlatBaseMesh = Mesh.CreateFromSubDControlNet(sprayPathSubD);
                MeshTopologyEdgeList edgeList = sprayFlatBaseMesh.TopologyEdges;
                for (int i = 0; i < edgeList.Count; i++)
                {
                    int[] connectedFaces = edgeList.GetConnectedFaces(i);

                    if (connectedFaces.Length > 1)
                    {
                        InteriorEdges.Add(new LineCurve(edgeList.EdgeLine(i)));
                    }

                }

            }


            Mesh spraySmoothBaseMesh = Mesh.CreateFromSubD(node.SprayBaseSubD, pathAdhesiveness);
            List<Curve> sprayPath = new List<Curve>();
            foreach (Curve crv in InteriorEdges)
            {
                sprayPath.Add(crv.PullToMesh(spraySmoothBaseMesh, 0.01));
            }
            return sprayPath;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Properties.Resources.SprayPath;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("18638C1D-27E5-421A-8949-3D7AC741417A"); }
        }
    }
}