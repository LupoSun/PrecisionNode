using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace PrecisionNode
{
    public class DeconstructNode : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public DeconstructNode()
          : base("Deconstruct Node", "DN",
              "Extract node information",
              "PrecisionNode", "Node Designer")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Node", "N", "The Node to extract information from", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Branch Number", "BN", "The Index Number of a NodeBranch", GH_ParamAccess.list);
            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Centre Point", "CPt", "The centre point of the node", GH_ParamAccess.item);
            pManager.AddPointParameter("Branch Start Points", "BSPt", "The points where the NodeBranches start", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Branch Start Planes", "BSPl", "The planes where the NodeBranches start", GH_ParamAccess.list);
            pManager.AddNumberParameter("Branch Radii", "BR", "The Radii of the NodeBranches", GH_ParamAccess.list);
            pManager.AddNumberParameter("Core Thread Wall Thickness", "CTWT", "The wall thickness around the thread", GH_ParamAccess.item);
            pManager.AddNumberParameter("Core Wall Thickness", "CWT", "The wall thickness of the hollow core", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Node node = new Node();
            List<int> branchNums = new List<int>();

            Point3d centrePoint = new Point3d();
            List<Point3d> branchStartPoints = new List<Point3d>();
            List<Plane> branchStartPlanes = new List<Plane>();
            List<double> branchRadii = new List<double>();
            double coreThreadWallThickness = double.NaN;
            double coreWallThickness = double.NaN;


            bool success = DA.GetData(0, ref node);
            bool successBranchNums = DA.GetDataList<int>(1, branchNums);
            if (success && !successBranchNums)
            {
                centrePoint = node.NodeCentrePoint;
                for (int i = 0; i < node.NodeBranches.Count; i++)
                {
                    if (node.NodeBranchDict.ContainsKey(i))
                    {
                        branchStartPoints.Add(node.NodeBranchDict[i].BranchStartPoint);
                        branchStartPlanes.Add(node.NodeBranchDict[i].BranchStartPlane);
                        branchRadii.Add(node.NodeBranchDict[i].Radius);
                        coreThreadWallThickness = node.CoreThreadWallThickness;
                        coreWallThickness = node.CoreWallThickness;
                    }
                    else AddRuntimeMessage(GH_RuntimeMessageLevel.Error, String.Format("Node{0} compromised, please consider reconstruct the Node", node.NodeNum));
                    
                } 
            }else if (success && successBranchNums)
            {
                centrePoint = node.NodeCentrePoint;

                foreach (int branchNum in branchNums)
                {
                    if (node.NodeBranchDict.ContainsKey(branchNum))
                    {
                        int i = branchNum;
                        branchStartPoints.Add(node.NodeBranchDict[i].BranchStartPoint);
                        branchStartPlanes.Add(node.NodeBranchDict[i].BranchStartPlane);
                        branchRadii.Add(node.NodeBranchDict[i].Radius);
                        coreThreadWallThickness = node.CoreThreadWallThickness;
                        coreWallThickness = node.CoreWallThickness;
                    }
                    else AddRuntimeMessage(GH_RuntimeMessageLevel.Error, String.Format("Node{0} does not have the Nodebranch{1}", node.NodeNum, branchNum));
                }
            }

            DA.SetData(0, centrePoint);
            DA.SetDataList(1, branchStartPoints);
            DA.SetDataList(2, branchStartPlanes);
            DA.SetDataList(3, branchRadii);

            if (double.IsNaN(coreThreadWallThickness) || double.IsNaN(coreWallThickness)) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, String.Format("Node{0} does not have a CoreGeometry yet", node.NodeNum));
            else
            {
                DA.SetData(4, coreThreadWallThickness);
                DA.SetData(5, coreWallThickness);
            }
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
                return Properties.Resources.DeconstructNode;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("98FADD17-F7C3-4CF5-B6C4-90A9928CA1DE"); }
        }
    }
}