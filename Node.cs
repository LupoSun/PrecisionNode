using Rhino.Geometry;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects.Custom;

namespace PrecisionNode
{
    public class Node
    {
        private List<NodeBranch> nodeBranches;
        private SubD nodeSimpleSubD;
        private SubD coatingBaseSubD;
        private int nodeNum;
        private Brep coreGeometry;
        private double coreWallThickness;
        private double coreThreadWallThickness;
        private Brep coatingGeometry;
        private List<Curve> sprayPath;

        //REDUNDENT
        private Dictionary<int, double> branchRadii;
        private Dictionary<int, Vector3d> branchLoadVectors;
        private Dictionary<int, Point3d> branchStartPoints;
        private Dictionary<int, Plane> branchStartPlanes;
        //REDUNDENT

        private Dictionary<int, NodeBranch> nodeBranchDict;
        private Point3d nodeCentrePoint;
        


        public List<NodeBranch> NodeBranches { get { return nodeBranches; } }
        public SubD NodeSimpleSubD { get { return nodeSimpleSubD; } }
        public Brep CoreGeometry { get { return coreGeometry; } }
        public SubD CoatingBaseSubD { get { return coatingBaseSubD; } }
        public int NodeNum { get { return nodeNum; } set { nodeNum = value; }}
        public double CoreThreadWallThickness { get { return coreThreadWallThickness; } set { coreThreadWallThickness = value; } }
        public double CoreWallThickness { get { return coreWallThickness; } set { coreWallThickness = value; } }
        public List<Curve> SprayPath { get { return sprayPath; } set { sprayPath = value; } }

        //REDUNDENT
        public Dictionary<int, double> BranchRadii { get { return branchRadii; } }
        public Dictionary<int, Vector3d> BranchLoadVectors { get { return branchLoadVectors; } }
        public Dictionary<int, Point3d> BranchStartPoints { get { return branchStartPoints; } }
        public Dictionary<int, Plane> BranchStartPlanes { get { return branchStartPlanes; } }
        //REDUNDENT

        public Dictionary<int, NodeBranch> NodeBranchDict { get { return nodeBranchDict; } }
        public Point3d NodeCentrePoint { get { return nodeCentrePoint; } set { nodeCentrePoint = value; } }
        

        /// <summary>
        /// Constructor for a empty Node object
        /// </summary>
        public Node(int nodeNum = -1)
        {
            nodeBranches = new List<NodeBranch>();
            nodeSimpleSubD = null;
            coatingBaseSubD = null;
            coreGeometry = null;
            coreWallThickness = double.NaN;
            coreThreadWallThickness = double.NaN;
            coatingGeometry = null;
            this.nodeNum = nodeNum;
            sprayPath = new List<Curve>();

            //REDUNDENT
            branchRadii = new Dictionary<int, double>();
            branchLoadVectors = new Dictionary<int, Vector3d>();
            branchStartPoints = new Dictionary<int, Point3d>();
            branchStartPlanes = new Dictionary<int, Plane>();
            //REDUNDENT

            nodeCentrePoint = new Point3d();
            nodeBranchDict = new Dictionary<int, NodeBranch>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="nodeBranches"></param>
        /// <param name="nodeNum"></param>
        public Node(List<NodeBranch> nodeBranches, int nodeNum = -1)
        {
            this.nodeBranches = nodeBranches;
            nodeSimpleSubD = null;
            coatingBaseSubD = null;
            coreGeometry = null;
            coreWallThickness = double.NaN;
            coreThreadWallThickness = double.NaN;
            coatingGeometry = null;
            this.nodeNum = nodeNum;
            sprayPath = new List<Curve>();

            //REDUNDENT
            branchRadii = new Dictionary<int, double>();
            branchLoadVectors = new Dictionary<int, Vector3d>();
            branchStartPoints = new Dictionary<int, Point3d>();
            branchStartPlanes = new Dictionary<int, Plane>();
            //REDUNDENT

            nodeCentrePoint = new Point3d();
            nodeBranchDict = new Dictionary<int, NodeBranch>();
        }

        /// <summary>
        /// Create a simple SubD representation of the Node using the internal list of NodeBranches
        /// </summary>
        public void CreateNodeSimpleSubD()
        {
            List<SubD> branchSubDs = new List<SubD>();
            foreach (NodeBranch branch in nodeBranches)
            {
                branch.CreateSimpleSubD();
                branchSubDs.Add(branch.BranchSimpleSubD);
            }

            nodeSimpleSubD = SubD.JoinSubDs(branchSubDs, 0.01, false)[0];
        }

        public void CreateCoreGeometry(double wallThickness, double threadWallThickness, double threadLength)
        {
            //store information into the Node
            coreWallThickness = wallThickness;
            coreThreadWallThickness = threadWallThickness;

            //Offset the node's simple SubD and cap all the holes into a solid Brep
            SubD outerShell = this.nodeSimpleSubD.Offset(threadWallThickness, false);
            //Assign the outerShell as the base SubD surface for spraying
            this.coatingBaseSubD = outerShell;
            Brep solid = outerShell.ToBrep(SubDToBrepOptions.DefaultPacked);
            solid = solid.CapPlanarHoles(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance * 10); //double the tolerance to make sure the capping succeed

            SubD innerShell = outerShell.Offset(-wallThickness, false);
            Brep innerShellBrep = innerShell.ToBrep(SubDToBrepOptions.DefaultPacked);

            //Flip all the BranchStartPlane and put them into a list
            List<Plane> splittingPlanes = new List<Plane>();
            foreach (NodeBranch nodeBranch in nodeBranches)
            {
                Plane splittingPlane = new Plane(nodeBranch.BranchStartPlane);
                splittingPlane.Flip();
                splittingPlanes.Add(splittingPlane);
            }

            List<Brep> cylindersBreps = new List<Brep>();
            for (int i = 0; i < nodeBranches.Count; i++)
            {
                //Construct planar Brep with R = 1.5 * BranchRadius for splitting the innerShell
                NurbsCurve brepBoundary = NurbsCurve.CreateFromCircle(new Circle(splittingPlanes[i], 1.5 * nodeBranches[i].Radius));
                Brep splittingBrep = Brep.CreatePlanarBreps(brepBoundary, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)[0];

                //move the cutting Brep to its position
                Vector3d moveVector = nodeBranches[i].BranchStartPlane.Normal;
                moveVector.Unitize();
                moveVector *= threadLength;
                Transform move = Transform.Translation(moveVector);
                splittingBrep.Transform(move);

                Brep[] resultBrep = innerShellBrep.Trim(splittingBrep, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                innerShellBrep = resultBrep[0];

                //The length of the cylinder is 1.25 * threadLength and move the cylinder 0.125 of the threadLength outwards
                Brep cylinderBrep = new Cylinder(new Circle(splittingPlanes[i], nodeBranches[i].Radius), -(threadLength + threadLength / 4)).ToBrep(true, true);
                Transform moveCylinder = Transform.Translation(-(moveVector / 8));
                cylinderBrep.Transform(moveCylinder);
                cylindersBreps.Add(cylinderBrep);

            }

            Brep innerSolid = innerShellBrep.CapPlanarHoles(RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            cylindersBreps.Add(innerSolid);
            Brep[] booleanUnionResult =
                Brep.CreateBooleanUnion(cylindersBreps, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            innerSolid = booleanUnionResult[0];

            Brep[] booleanDifferenceResult = Brep.CreateBooleanDifference(solid, innerSolid, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            this.coreGeometry = booleanDifferenceResult[0];

        }

        /// <summary>
        /// Initialise the information of a node base on the current list of the NodeBranches
        /// </summary>
        public void InitialiseNodeInfo()
        {
            if (nodeBranches.Count > 0)
            {
                //set the centre point of the node
                nodeCentrePoint = nodeBranches[0].CentrePoint;

                foreach (NodeBranch nodeBranch in nodeBranches)
                {
                    int BranchNum = nodeBranch.BranchNum;
                    NodeBranchDict.Add(BranchNum, nodeBranch);

                    //REDUNDENT
                    branchRadii.Add(BranchNum, nodeBranch.Radius);
                    branchStartPoints.Add(BranchNum, nodeBranch.BranchStartPoint);
                    branchLoadVectors.Add(BranchNum, nodeCentrePoint - nodeBranch.BranchStartPoint);
                    branchStartPlanes.Add(BranchNum, nodeBranch.BranchStartPlane);
                    //REDUNDENT
                }

            }
        }
    }
}
