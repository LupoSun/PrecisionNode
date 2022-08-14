using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace PrecisionNode
{
    public class SimpleSurface : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public SimpleSurface()
          : base("Simple Surface", "SS",
              "Construct the simple surface representation of the nodes as SubD and Brep",
              "PrecisionNode", "Node Smith")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Node", "N", "The constructed Node objects", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Packed Brep", "Packed", "If the Brep output is packed", GH_ParamAccess.item,true);
            pManager[1].Optional = true;
            pManager.AddBooleanParameter("Commpute Quad Mesh", "CQM", "If compute the quad mesh representation of the surface", GH_ParamAccess.item, false);
            pManager[2].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddSubDParameter("SubD", "SubD", "SubD Surface", GH_ParamAccess.item);
            pManager.AddBrepParameter("Brep", "Brep", "Brep Surface", GH_ParamAccess.item);
            pManager.AddMeshParameter("Quad Mesh", "QM", "The simple Quad Mesh surface for the spay process", GH_ParamAccess.item);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Node node = null;
            bool packed = true;
            bool computeQuadMesh = false;
            bool successNode = DA.GetData(0, ref node);
            bool successPacked = DA.GetData(1, ref packed);
            bool successComputeQuadMesh = DA.GetData(2, ref computeQuadMesh);

            Mesh simpleSurfaceQuadMesh = null;
            SubD subD = null;
            Brep brep = null;

            
                if (node.NodeSimpleSubD == null)
                {
                    node.CreateNodeSimpleSubD();
                }

                subD = node.NodeSimpleSubD;

                if (packed) brep = subD.ToBrep(SubDToBrepOptions.DefaultPacked);
                else brep = subD.ToBrep(SubDToBrepOptions.Default);

            if (computeQuadMesh)
            {
                simpleSurfaceQuadMesh = Mesh.CreateFromSubD(subD, 4);
                simpleSurfaceQuadMesh = simpleSurfaceQuadMesh.QuadRemesh(new QuadRemeshParameters());
            }
            

            DA.SetData(0, subD);
            DA.SetData(1, brep);
            DA.SetData(2, simpleSurfaceQuadMesh);



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
                return Properties.Resources.SimpleSurface;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("ED27EA99-9B7B-4040-8857-F68CF064CD48"); }
        }
    }
}