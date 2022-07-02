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
        private SubD sprayBaseSubD;
        private int nodeNum;
        private Brep coreGeometry;
        private double coreWallThickness;
        private double coreThreadWallThickness;
        private Brep coatingGeometry;
        private List<Curve> sprayPath;
        


        public List<NodeBranch> NodeBranches { get { return nodeBranches; } }
        public SubD NodeSimpleSubD { get { return nodeSimpleSubD; } }
        public Brep CoreGeometry { get { return coreGeometry; } }
        public SubD SprayBaseSubD { get { return sprayBaseSubD; } }
        public List<Curve> SprayPath { 
            get { return sprayPath; }
            set { sprayPath = value; }
        }

        /// <summary>
        /// Constructor for a empty Node object
        /// </summary>
        public Node(int nodeNum = -1)
        {
            nodeBranches = new List<NodeBranch>();
            nodeSimpleSubD = null;
            sprayBaseSubD = null;
            coreGeometry = null;
            coreWallThickness = double.NaN;
            coreThreadWallThickness = double.NaN;
            coatingGeometry = null;
            this.nodeNum = nodeNum;
            sprayPath = new List<Curve>();
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
            sprayBaseSubD=null;
            coreGeometry = null;
            coreWallThickness = double.NaN;
            coreThreadWallThickness = double.NaN;
            coatingGeometry = null;
            this.nodeNum = nodeNum;
            sprayPath = new List<Curve>();
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
            this.sprayBaseSubD = outerShell;
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

                cylindersBreps.Add(new Cylinder(new Circle(splittingPlanes[i], nodeBranches[i].Radius), -(threadLength)).ToBrep(true, true));

            }

            Brep innerSolid = innerShellBrep.CapPlanarHoles(RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            cylindersBreps.Add(innerSolid);
            Brep[] booleanUnionResult =
                Brep.CreateBooleanUnion(cylindersBreps, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            innerSolid = booleanUnionResult[0];

            Brep[] booleanDifferenceResult = Brep.CreateBooleanDifference(solid, innerSolid, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            this.coreGeometry = booleanDifferenceResult[0];

        }
    }
}
