using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;


namespace PrecisionNode
{
    public class CoatingBaseSurface : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public CoatingBaseSurface()
          : base("Coating Base Surface", "The base SubD suface for the spay process",
              "Description",
              "PrecisionNode", "Node Smith")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Nodes", "N", "The Node objects to extract information from", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Packed Brep", "Packed", "If the Brep output is packed", GH_ParamAccess.item,true);
            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddSubDParameter("SubD", "S", "The base SubD surface for the spay process", GH_ParamAccess.item);
            pManager.AddBrepParameter("Brep", "B", "The base Brep surface for the spay process", GH_ParamAccess.item);
            pManager.AddMeshParameter("Quad Mesh", "QM", "The base Quad Mesh surface for the spay process", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Node node = null;
            bool packed = true;
            bool successNode = DA.GetData(0, ref node);
            bool successPacked = DA.GetData(1, ref packed);

            SubD CoatingBaseSubD = null;
            Brep CoatingBaseBrep = null;
            Mesh CoatingBaseQuadMesh = null;

            if (node.CoreGeometry == null) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, String.Format("Node {0} doesn't have a core geometry yet",
                node.NodeNum));
            else
            {
                CoatingBaseSubD = node.CoatingBaseSubD;

                if (packed) CoatingBaseBrep = CoatingBaseSubD.ToBrep(SubDToBrepOptions.DefaultPacked);
                else CoatingBaseBrep = CoatingBaseSubD.ToBrep(SubDToBrepOptions.Default);

                CoatingBaseQuadMesh = Mesh.CreateFromSubD(node.CoatingBaseSubD, 4);
                CoatingBaseQuadMesh = CoatingBaseQuadMesh.QuadRemesh(new QuadRemeshParameters());
            }

            DA.SetData(0, CoatingBaseSubD);
            DA.SetData(1, CoatingBaseBrep);
            DA.SetData(2, CoatingBaseQuadMesh);

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
                return Properties.Resources.CoatingBaseSurface;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("B1B4E4DE-0BBF-45F1-B9CD-F2CAEDC840A2"); }
        }
    }
}