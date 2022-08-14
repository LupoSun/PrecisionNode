using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino;

namespace PrecisionNode
{
    public class CoatingGeometry : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public CoatingGeometry()
          : base("Coating Geometry", "CG",
              "Create the coating geometry of the node",
              "PrecisionNode", "Node Smith")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Node", "N", "The node object", GH_ParamAccess.item);
            pManager.AddMeshParameter("Coating Base Quad Mesh", "CBQM"
                , "The coating base quad mesh that is also used for karamba analysis", GH_ParamAccess.item);
            pManager.AddMeshParameter("Karamba Mesh", "KM",
                "The analysed triangular mesh with color information from Karamba", GH_ParamAccess.item);
            pManager.AddNumberParameter("Minimal Thickness", "MT",
                "The minimal thickness of the coating geometry", GH_ParamAccess.item,0.0f);
            pManager.AddNumberParameter("Displacement Multiplier", "DM",
                "The multiplier for the extend of the displacement", GH_ParamAccess.item,1.0f);
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Consider Neighbour Value", "CNV",
                "If the color value of a vertex includes the color values from the neighbouring verticex",
                GH_ParamAccess.item);
            pManager[5].Optional = true;

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Processed Node", "PN", "The node after the operation", GH_ParamAccess.item);
            pManager.AddMeshParameter("Quad Mesh", "QD", "The coating geometry as a closed quad Mesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double DOCABSOLUTETOLERANCE = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            Node node = null;
            Mesh coatingBaseQuadMesh = null;
            Mesh karambaMesh = null;
            double displacementMultiplier = 1.0;
            double minimalThickness = double.NaN;
            bool considerNeighbourValue = false;

            bool successNode = DA.GetData(0,ref node);
            bool successCoatingBaseQuadMesh = DA.GetData(1, ref coatingBaseQuadMesh);
            bool successKarambaMesh = DA.GetData(2, ref karambaMesh);
            bool successMinimalThickness = DA.GetData(3, ref minimalThickness);
            bool successDisplacementMultiplier = DA.GetData(4, ref displacementMultiplier);
            bool successSmooth = DA.GetData(5, ref considerNeighbourValue);

            if (!successDisplacementMultiplier) displacementMultiplier = 1.0;
            if (!successSmooth) considerNeighbourValue = false;

            Mesh morphedMesh=null;

            if (successNode && successCoatingBaseQuadMesh && successKarambaMesh && successMinimalThickness)
            {
                //Replicate the Mesh for processing
                morphedMesh = coatingBaseQuadMesh.DuplicateMesh();
                //Fail safe for the normals
                morphedMesh.RebuildNormals();

                //Commpute the bound of the RGB values of the vertices in the Karamba mesh
                List<int> colors = new List<int>();
                double start1 = karambaMesh.VertexColors.Min(x => x.R);
                double end1 = karambaMesh.VertexColors.Max(x => x.R);

                //Find the nacked vertices
                bool[] ifNaked = morphedMesh.GetNakedEdgePointStatus();

                //loop for vertex morph
                for (int i = 0; i < morphedMesh.Vertices.Count; i++)
                {
                    Vector3f normal = morphedMesh.Normals[i];
                    normal.Unitize();

                    float colorValue = float.NaN;
                    //If the algorithm should consider the color values from each vertex as well as its neighour vertices
                    if (considerNeighbourValue)
                    {
                        //Find the topological neighbours of the test points
                        List<Point3d> neighbouringPoints = new List<Point3d>();
                        int[] connectedEdgeIndices = coatingBaseQuadMesh.TopologyVertices.ConnectedEdges(i);
                        foreach (int index in connectedEdgeIndices)
                        {
                            neighbouringPoints.Add(coatingBaseQuadMesh.TopologyEdges.EdgeLine(i).PointAt(0));
                            neighbouringPoints.Add(coatingBaseQuadMesh.TopologyEdges.EdgeLine(i).PointAt(1));
                        }
                        //Remove duplicates
                        Point3d.CullDuplicates(neighbouringPoints, DOCABSOLUTETOLERANCE);
                        List<double> colorValues = new List<double>();
                        foreach (Point3d point in neighbouringPoints)
                        {
                            MeshPoint closestPt = karambaMesh.ClosestMeshPoint(point, 0.0);
                            colorValues.Add(karambaMesh.ColorAt(closestPt).R);
                        }
                        
                        colorValue = (float)Utilities.Remap(colorValues.Average(), end1, start1, 0, 1);
                    }
                    else
                    {
                        MeshPoint closestPt = karambaMesh.ClosestMeshPoint(morphedMesh.Vertices[i], 0.0);
                        colorValue = (float)Utilities.Remap(karambaMesh.ColorAt(closestPt).R, end1, start1, 0, 1);
                    }

                    Vector3f moveVector = normal * (float)minimalThickness + normal * colorValue * (float)displacementMultiplier;

                    morphedMesh.Vertices[i] = morphedMesh.Vertices[i] + moveVector;

                    //Move vertices back if the vertex is naked
                    if (ifNaked[i]) morphedMesh.Vertices[i] = coatingBaseQuadMesh.Vertices[i];

                    //Set the color of a vertex from the Karamba Mesh
                    MeshPoint closestMeshPt = karambaMesh.ClosestMeshPoint(morphedMesh.Vertices[i], 0.0);
                    morphedMesh.VertexColors.SetColor(i, karambaMesh.ColorAt(closestMeshPt));

                }

                //Close the mesh by appending the unmorphed mesh
                morphedMesh.Append(coatingBaseQuadMesh);

                node.CoatingGeometryMesh = morphedMesh;
                DA.SetData(0, node);
                DA.SetData(1, morphedMesh);


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
                return Properties.Resources.CoatingGeometry;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("C241C9C9-6EA6-4860-86D2-13AE40FFC797"); }
        }
    }
}