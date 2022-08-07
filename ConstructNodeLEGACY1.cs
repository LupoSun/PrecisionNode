using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrecisionNode
{
    public class ConstructNodeLEGACY1 : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public ConstructNodeLEGACY1()
          : base("Construct Node LEGACY1", "CN OLD",
            "Create nodes information object",
            "PrecisionNode", "Node Designer")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("BranchCurves",
                "BCrv",
                "The centre lines of the branches in the nodes",
                GH_ParamAccess.tree);
            pManager.AddNumberParameter("BranchRadius",
                "BR",
                "The radii of the branches",
                GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Nodes", "N", "The constructed nodes", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //Extra step of converting scripting DataTree to GH_Structure
            GH_Structure<GH_Curve> pipeLines;
            GH_Structure<GH_Number> pipeRadius;

            bool success1 = DA.GetDataTree(0, out pipeLines);
            bool success2 = DA.GetDataTree(1, out pipeRadius);

            if (success1 && success2)
            {
                //Operation carried out based on data tree structure
                List<SubD> nodeSubDs = new List<SubD>();
                List<Node> nodes = new List<Node>();

                for (int i = 0; i < pipeLines.Branches.Count; i++)
                {
                    List<Curve> branchPipeLines = new List<Curve>();
                    List<double> branchPipeRadius = new List<double>();
                    foreach (GH_Curve gh_PipeLines in pipeLines[i])
                    {
                        branchPipeLines.Add(gh_PipeLines.Value);
                    }

                    foreach (GH_Number gh_Radius in pipeRadius[i])
                    {
                        branchPipeRadius.Add(gh_Radius.Value);
                    }

                    double nodeRadius = branchPipeRadius.OrderByDescending(x => x).ToList()[0];
                    //Sort out the topological relationship of the pipe centre lines
                    List<Point3d> branchstartPoints;
                    Point3d centrePoint = Utilities.PipeLinesSort(branchPipeLines, out branchstartPoints);

                    //Initialize all the NodeBranch objects and put them into a Node object
                    Node node = new Node(i);

                    //First loop finds all the intersection curves and examine if the intersection is closed,
                    Dictionary<Point3d, Curve> intersectionDic = new Dictionary<Point3d, Curve>();
                    List<Point3d> loseEnds = new List<Point3d>();

                    foreach (Point3d branchStartPoint in branchstartPoints)
                    {
                        Curve intersection;
                        if (Utilities.CylinderIntersection(branchStartPoint, branchstartPoints, centrePoint, nodeRadius,
                                out intersection))
                        {
                            if (!intersection.IsClosed)
                            {
                                loseEnds.Add(intersection.PointAtStart);
                                loseEnds.Add(intersection.PointAtEnd);
                            }

                            intersectionDic.Add(branchStartPoint, intersection);
                        }
                    }

                    //compute the average position of all the lose ends
                    Point3d sum = new Point3d(0, 0, 0);
                    foreach (Point3d point in loseEnds)
                    {
                        sum += point;
                    }

                    Point3d averagePosition = sum / loseEnds.Count;

                    //second loop constructs the nodes with the (bridged) intersection curves
                    int branchNum = 0;
                    foreach (KeyValuePair<Point3d, Curve> kvp in intersectionDic)
                    {
                        Point3d branchstartPoint = kvp.Key;
                        Curve intersection = kvp.Value;

                        //Find the corresponding index for radius
                        int IndexOfRadius = branchstartPoints.IndexOf(branchstartPoint);
                        //Fail safe, if the index can't be found
                        if (IndexOfRadius == -1) IndexOfRadius = branchPipeRadius.Count - 1;
                        if (IndexOfRadius > branchPipeRadius.Count - 1) IndexOfRadius = branchPipeRadius.Count - 1;
                        double branchRadius = branchPipeRadius[IndexOfRadius];

                        NodeBranch nodeBranch = new NodeBranch(branchstartPoint, centrePoint, intersection, branchRadius,
                            branchNum);
                        branchNum++;

                        if (!kvp.Value.IsClosed)
                        {
                            nodeBranch.AddAveragePosition(averagePosition);
                        }

                        node.NodeBranches.Add(nodeBranch);
                    }

                    node.InitialiseNodeInfo();
                    nodes.Add(node);

                }

                DA.SetDataList(0, nodes);
            }
            else AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to collect data");
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.ConstructNodes;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("2484DB25-8DED-46EA-8C4B-F2333CF98993");
    }
}