using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;


namespace PrecisionNode
{
    public class CoreGeometry : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public CoreGeometry()
          : base("Core Geometry", "CG",
              "Construct the core geometry of the nodes as solid Brep",
              "PrecisionNode", "Node Smith")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Nodes", "N", "The constructed Node objects", GH_ParamAccess.list);
            pManager.AddNumberParameter("WallThickness", "WT", "The wall thickness of the core", GH_ParamAccess.item);
            pManager.AddNumberParameter("ThreadWallThickness", "TWT", "The wall thickness at the areas of the threads",
                GH_ParamAccess.item);
            pManager.AddNumberParameter("ThreadLength", "TL", "The length of the threads", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("CoreGeometry", "CG", "The core geometry as Brep", GH_ParamAccess.list);
            pManager.AddGenericParameter("ProcessedNodes", "PN", "The Node objects after being processed",
                GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Node> nodes = new List<Node>();
            double wallThickness = Double.NaN;
            double threadWallThickness = Double.NaN;
            double threadLength = Double.NaN;

            bool success1 = DA.GetDataList("Nodes", nodes);
            bool success2 = DA.GetData("WallThickness", ref wallThickness);
            bool success3 = DA.GetData("ThreadWallThickness", ref threadWallThickness);
            bool success4 = DA.GetData("ThreadLength", ref threadLength);

            List<Brep> coreGeometries = new List<Brep>();
            if (success1 && success2 && success3 && success4)
            {
                foreach (Node node in nodes)
                {
                    if (node.NodeSimpleSubD == null) node.CreateNodeSimpleSubD();

                    node.CreateCoreGeometry(wallThickness, threadWallThickness, threadLength);
                    coreGeometries.Add(node.CoreGeometry);
                }
            } else AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to collect data");
        
            

            bool success5 = DA.SetDataList("CoreGeometry", coreGeometries);
            bool success6 = DA.SetDataList("ProcessedNodes", nodes);
            if (!(success5 && success6)) AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to output data");
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
                return Properties.Resources.CoreGeometry;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("BD4114EE-67D8-4A0B-A14F-68E79D2B5B7F"); }
        }
    }
}