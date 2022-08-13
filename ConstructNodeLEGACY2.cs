using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrecisionNode
{
    public class ConstructNodeLEGACY2 : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public ConstructNodeLEGACY2()
          : base("Construct Node LEGACY2", "CN2",
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
                GH_ParamAccess.list);
            pManager.AddNumberParameter("BranchRadius",
                "BR",
                "The radii of the branches",
                GH_ParamAccess.list);
            pManager.AddIntegerParameter("Node Number", "NNum", "The number of the node, by default 0", GH_ParamAccess.item);
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Nodes", "N", "The constructed nodes", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            //read in the data from the inputs
            List<Curve> pipeLines = new List<Curve>();
            List<double> pipeRadii = new List<double>();
            int nodeNum = 0;

            bool successPipeLines = DA.GetDataList(0, pipeLines);
            bool successPipeRadii = DA.GetDataList(1, pipeRadii);
            bool successNodeNum = DA.GetData(2, ref nodeNum);
            if (!successNodeNum) nodeNum = 0;

            Node node = null;

            if (successPipeLines & successPipeRadii)
            {
                bool openIntersections = false;
                double nodeRadius = pipeRadii.OrderByDescending(x => x).ToList()[0];
                //Sort out the topological relationship of the pipe centre lines
                List<Point3d> branchstartPoints;
                Point3d centrePoint = Utilities.PipeLinesSort(pipeLines, out branchstartPoints);

                //Initialize all the NodeBranch objects and put them into a Node object
                node = new Node(nodeNum);

                //First loop finds all the intersection curves and examine if the intersection is closed,
                Dictionary<Point3d, Curve> intersectionDic = new Dictionary<Point3d, Curve>();
                List<Point3d> loseEnds = new List<Point3d>();
                List<Vector3d> branchVectors = new List<Vector3d>();

                foreach (Point3d branchStartPoint in branchstartPoints)
                {
                    branchVectors.Add(centrePoint - branchStartPoint);
                    Curve intersection;
                    if (Utilities.CylinderIntersection(branchStartPoint, branchstartPoints, 
                        centrePoint, nodeRadius, out intersection))
                    {
                        if (!intersection.IsClosed)
                        {
                            openIntersections = true;
                            loseEnds.Add(intersection.PointAtStart);
                            loseEnds.Add(intersection.PointAtEnd);
                        }

                        intersectionDic.Add(branchStartPoint, intersection);
                    }
                }

                //compute the average position of all the lose ends and the average vector
                Point3d sum = new Point3d(0, 0, 0);
                foreach (Point3d point in loseEnds)
                {
                    sum += point;
                }
                Point3d averagePosition = sum / loseEnds.Count;

                Vector3d sumVector = new Vector3d(0,0,0);
                foreach(Vector3d vector in branchVectors)
                {
                    sumVector += vector;
                }

                if (openIntersections)
                {
                    //pull the everage point onto the sphere with the radius of the node
                    /*
                    NurbsSurface nurbSphere = NurbsSurface.CreateFromSphere(new Sphere(centrePoint, nodeRadius));
                    double u, v;
                    if (nurbSphere.ClosestPoint(averagePosition, out u, out v))
                    {
                        averagePosition = nurbSphere.PointAt(u, v);
                    }
                    */
                    Brep brepShpere = Brep.CreateFromSphere(new Sphere(centrePoint, nodeRadius));
                    Point3d[] averagePositions = Intersection.ProjectPointsToBreps(new Brep[] { brepShpere },
                        new Point3d[] { averagePosition },
                        -sumVector,
                        Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    if (averagePositions != null) averagePosition = averagePositions[0];
                }

                //second loop constructs the nodes with the (bridged) intersection curves
                int branchNum = 0;
                foreach (KeyValuePair<Point3d, Curve> kvp in intersectionDic)
                {
                    Point3d branchstartPoint = kvp.Key;
                    Curve intersection = kvp.Value;

                    //Find the corresponding index for radius
                    int IndexOfRadius = branchstartPoints.IndexOf(branchstartPoint);
                    //Fail safe, if the index can't be found
                    if (IndexOfRadius == -1) IndexOfRadius = pipeRadii.Count - 1;
                    if (IndexOfRadius > pipeRadii.Count - 1) IndexOfRadius = pipeRadii.Count - 1;
                    double branchRadius = pipeRadii[IndexOfRadius];

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
                DA.SetData(0, node);
            }
            else AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to collect data");
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.ConstructNodesLEGACY;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("4941A65F-E0B6-427E-AF27-B4D21EF237C8");
    }
}