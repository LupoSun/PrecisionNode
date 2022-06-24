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
          : base("SimpleSurface", "SS",
              "Construct the simple surface representation of the nodes as SubD and Brep",
              "PrecisionNode", "NodeSmith")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Nodes", "N", "The constructed Node objects", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Packed Brep", "Packed", "If the Brep output is packed", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddSubDParameter("SubD", "SubD", "SubD Surface", GH_ParamAccess.list);
            pManager.AddBrepParameter("Brep", "Brep", "Brep Surface", GH_ParamAccess.list);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Node> nodes = new List<Node>();
            bool packed = true;
            bool success1 = DA.GetDataList(0, nodes);
            bool success2 = DA.GetData(1, ref packed);

            List<SubD> subDs = new List<SubD>();
            List<Brep> Breps = new List<Brep>();

            foreach(Node node in nodes)
            {
                if (node.NodeSimpleSubD == null)
                {
                    node.CreateNodeSimpleSubD();
                }

                SubD subD = node.NodeSimpleSubD;
                subDs.Add(subD);

                if (packed) Breps.Add(subD.ToBrep(SubDToBrepOptions.DefaultPacked));
                else Breps.Add(subD.ToBrep(SubDToBrepOptions.Default));
            }

            DA.SetDataList(0, subDs);
            DA.SetDataList(1, Breps);



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