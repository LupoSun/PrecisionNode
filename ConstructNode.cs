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
    public class ConstructNode : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public ConstructNode()
          : base("Construct Node", "CN",
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
            double DOCABSOLUTETOLERANCE = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            int nodeNum = 0;

            bool successPipeLines = DA.GetDataList(0, pipeLines);
            bool successPipeRadii = DA.GetDataList(1, pipeRadii);
            bool successNodeNum = DA.GetData(2, ref nodeNum);
            if (!successNodeNum) nodeNum = 0;

            Node node = null;

            if (successPipeLines & successPipeRadii)
            {
                double nodeRadius = pipeRadii.OrderByDescending(x => x).ToList()[0];
                double stichDistance = nodeRadius / 2;

                //Sort out the topological relationship of the pipe centre lines
                List<Point3d> branchstartPoints;
                Point3d centrePoint = Utilities.PipeLinesSort(pipeLines, out branchstartPoints);

                //Initialize all the NodeBranch objects and put them into a Node object
                node = new Node(nodeNum);

                bool openIntersectionExists = false;
                Point3d branchWithStichPotencial = new Point3d();
                List<Point3d> loseEnds;
                List<Vector3d> branchVectors;
                bool stichNeeded = false;
                Point3d pointToSwap1 = new Point3d();
                Point3d pointToSwap2 = new Point3d();
                Point3d stichPosition = new Point3d();
                Point3d averagePosition = new Point3d();

                //1. Find all the intersection curves and examine if the intersection is closed
                //and and putting all the lose ends into a dictionary
                Dictionary<Point3d, Curve> intersectionDict = new Dictionary<Point3d, Curve>();
                Dictionary<Point3d, List<Point3d>> loseEndsDict = new Dictionary<Point3d, List<Point3d>>();

                foreach (Point3d branchStartPoint in branchstartPoints)
                {
                    Curve intersection;
                    if (Utilities.CylinderIntersection(branchStartPoint, branchstartPoints,
                      centrePoint, nodeRadius, out intersection))
                    {
                        if (!intersection.IsClosed)
                        {
                            openIntersectionExists = true;
                            Point3d loseEndStart = intersection.PointAtStart;
                            Point3d loseEndEnd = intersection.PointAtEnd;
                            loseEndsDict.Add(branchStartPoint, new List<Point3d> { loseEndStart, loseEndEnd });
                        }
                        intersectionDict.Add(branchStartPoint, intersection);
                    }
                }

                if (openIntersectionExists)
                {
                    //2. Construct the list of lose ends depending on if one stich needed
                    branchWithStichPotencial = loseEndsDict.OrderBy(x => x.Value[0].DistanceTo(x.Value[1])).First().Key;
                    loseEnds = new List<Point3d>();
                    branchVectors = new List<Vector3d>();
                    pointToSwap1 = new Point3d();
                    pointToSwap2 = new Point3d();

                    if (loseEndsDict[branchWithStichPotencial][0].DistanceTo(loseEndsDict[branchWithStichPotencial][1]) < stichDistance)
                    {
                        stichNeeded = true;
                        pointToSwap1 = loseEndsDict[branchWithStichPotencial][0];
                        pointToSwap2 = loseEndsDict[branchWithStichPotencial][1];
                        stichPosition = (pointToSwap1 + pointToSwap2) / 2;
                        loseEnds.Add(stichPosition);

                        foreach (KeyValuePair<Point3d, List<Point3d>> kvp in loseEndsDict)
                        {
                            if (!kvp.Key.EpsilonEquals(branchWithStichPotencial, DOCABSOLUTETOLERANCE))
                            {
                                foreach (Point3d point in kvp.Value)
                                {
                                    if (!point.EpsilonEquals(pointToSwap1, DOCABSOLUTETOLERANCE)
                                      && !point.EpsilonEquals(pointToSwap2, DOCABSOLUTETOLERANCE))
                                    {
                                        loseEnds.Add(point);
                                    }
                                }
                                branchVectors.Add(centrePoint - kvp.Key);
                            }
                        }
                    }
                    else
                    {
                        foreach (KeyValuePair<Point3d, List<Point3d>> kvp in loseEndsDict)
                        {
                            loseEnds.AddRange(kvp.Value);
                            branchVectors.Add(centrePoint - kvp.Key);
                        }
                    }

                    //3. Compute the average position of all the lose ends and the average vector
                    Point3d sum = new Point3d(0, 0, 0);
                    loseEnds = Point3d.CullDuplicates(loseEnds, DOCABSOLUTETOLERANCE).ToList();
                    foreach (Point3d point in loseEnds)
                    {
                        sum += point;
                    }
                    averagePosition = sum / loseEnds.Count;

                    Vector3d sumVector = new Vector3d(0, 0, 0);
                    foreach (Vector3d vector in branchVectors)
                    {
                        sumVector += vector;
                    }

                    //4. Peoject the average position along the sum vector of all branches with unclosed intersection

                    Brep brepShpere = Brep.CreateFromSphere(new Sphere(centrePoint, nodeRadius));
                    Point3d[] averagePositions = Intersection.ProjectPointsToBreps(new Brep[] { brepShpere },
                      new Point3d[] { averagePosition },
                      -sumVector,
                      DOCABSOLUTETOLERANCE);
                    if (averagePositions != null) averagePosition = averagePositions[0];

                }
                //5. second loop constructs the nodes with the (bridged) intersection curves
                int branchNum = 0;
                foreach (KeyValuePair<Point3d, Curve> kvp in intersectionDict)
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

                    if (openIntersectionExists)
                    {
                        if (!kvp.Value.IsClosed)
                        {
                            if (stichNeeded)
                            {
                                List<Point3d> pointToBeSubtituded = new List<Point3d> { pointToSwap1, pointToSwap2 };
                                if (kvp.Key.EpsilonEquals(branchWithStichPotencial, DOCABSOLUTETOLERANCE))
                                {
                                    nodeBranch.InitialiseIntersectionCorners();
                                }
                                else
                                {
                                    nodeBranch.AddAveragePosition(averagePosition);
                                }
                                nodeBranch.SubtitudeIntersectionCorners(stichPosition, pointToBeSubtituded);
                            }
                            else
                            {
                                nodeBranch.AddAveragePosition(averagePosition);
                            }
                        }
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
        protected override System.Drawing.Bitmap Icon => Properties.Resources.ConstructNodes;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("3D4C0A88-B15B-4E6F-9FF8-F108AD6F5EB8");
    }
}