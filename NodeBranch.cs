using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using static PrecisionNode.Utilities;

namespace PrecisionNode
{
    public class NodeBranch
    {
        private Curve centreLine;
        private readonly Point3d centrePoint;
        private Point3d branchStartPoint;
        private Curve cylinderIntersection;
        private double radius;
        private List<Point3d> intersectionCorners;
        private Brep branchLoft;
        private SubD branchSimpleSubD;
        private readonly int branchNum;
        private readonly Plane branchStartPlane;

        public Plane BranchStartPlane { get { return branchStartPlane; } }
        public Point3d BranchStartPoint { get { return branchStartPoint; } }
        public Point3d CentrePoint { get { return centrePoint; } }
        public double Radius { get { return radius; } }
        public SubD BranchSimpleSubD { get { return branchSimpleSubD; } }
        public Brep BranchLoft { get { return branchLoft; } }

        public Curve CylinderIntersection { get { return cylinderIntersection; } }

        /// <summary>
        /// Automatically add more intersectionCorners to the internal list
        /// </summary>
        /// <param name="cornerToAdd"></param>
        public void AddIntersectionCorners(Point3d cornerToAdd)
        {
            List<Point3d> cornersToAdd = new List<Point3d>{cornerToAdd};
            intersectionCorners = GetIntersectionCorners(cylinderIntersection, branchStartPlane,40, cornersToAdd);
            //intersectionCorners.Add(cornerToAdd);
            intersectionCorners = PlaneRadialPointSort(intersectionCorners, branchStartPlane);
        }
        /// <summary>
        /// Create the SubD and Loft Geometry and store them in the NodeBranch object
        /// </summary>
        public void CreateSimpleSubD()
        {
            if (intersectionCorners == null)
            {
                //get the rebuilt intersection c orners
                intersectionCorners = GetIntersectionCorners(cylinderIntersection, branchStartPlane);
            }
            //sort the corners radially
            intersectionCorners = PlaneRadialPointSort(intersectionCorners, branchStartPlane);
            LoftBranch(intersectionCorners, radius, branchStartPlane, out branchLoft, out branchSimpleSubD);
        }
        public NodeBranch(Point3d startPoint, Point3d centrePoint, Curve cylinderIntersection, double radius, int branchNum)
        {
            //setting up the basic attributes
            branchStartPoint = startPoint;
            this.centrePoint = centrePoint;
            this.cylinderIntersection = cylinderIntersection;
            this.radius = radius;
            this.branchNum = branchNum;

            //compute the indirect attributes
            //get the centre line of the branch
            centreLine = new LineCurve(startPoint, centrePoint);
            //compute the plane on the outter reach perpendicular to the centre line
            branchStartPlane = new Plane(branchStartPoint, centrePoint - branchStartPoint);
            




        }
        /*
        /// <summary>
        /// By having the PolyCurve of the cylinder intersection, this function rebuilds the curve into
        /// PolyLineCurves and retrieve the corner points
        /// </summary>
        /// <param name="cylinderIntersection"></param>
        /// <param name="basePlane"></param>
        /// <returns></returns>
        public static List<Point3d> GetIntersectionCorners(Curve cylinderIntersection, Plane basePlane)
        {
            Curve[] curveSegments;
            //check if the intersection is a PolyCurve
            if (cylinderIntersection is PolyCurve intersection)
            {
                curveSegments = intersection.Explode();
            }
            else { curveSegments = new Curve[] { cylinderIntersection }; }

            List<NurbsCurve> rebuiltSegments = new List<NurbsCurve>();
            List<Point3d> intersectionCorners = new List<Point3d>();

            Transform projection = Transform.PlanarProjection(basePlane);

            foreach (Curve curve in curveSegments)
            {
                //measure the angle between two vectors from the endpoints to the plane origin with respect of normal vector
                Vector3d vStart = curve.PointAtStart - basePlane.Origin;
                vStart.Transform(projection);
                Vector3d vEnd = curve.PointAtEnd - basePlane.Origin;
                vEnd.Transform(projection);
                int segmentNum = (int)(Vector3d.VectorAngle(vStart, vEnd) / (Math.PI / 180 * (40 - RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)));
                //to have a minimum segmentNum for rebuilding
                if (segmentNum == 0) segmentNum++;
                //rebuild the Curve acccording to the determined segment number
                NurbsCurve rebuilt = curve.Rebuild(segmentNum + 1, 1, false);
                if (rebuilt != null) rebuiltSegments.Add(rebuilt);
            }

            //add all the end points of the segments into the list as a failsafe for the next step
            foreach (Curve curve in rebuiltSegments)
            {
                intersectionCorners.Add(curve.PointAtEnd);
                intersectionCorners.Add(curve.PointAtStart);
            }

            //get the joined PolyLine after rebuilding
            PolylineCurve joinedRebuilt = (PolylineCurve)Curve.JoinCurves(rebuiltSegments)[0];

            //find all the corners
            for (int i = 0; i < joinedRebuilt.PointCount - 1; i++)
            {
                intersectionCorners.Add(joinedRebuilt.Point(i));
            }

            //clean up and remove all the duplicate points
            Point3d[] pointDupRemoved = Point3d.CullDuplicates(intersectionCorners, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

            return pointDupRemoved.ToList();
        }

        

        /// <summary>
        /// Sort the corners radially base on the branchStartPlane
        /// </summary>
        /// <param name="toSort"></param>
        /// <param name="basePlane"></param>
        /// <returns></returns>
        public static List<Point3d> PlaneRadialPointSort(List<Point3d> toSort, Plane basePlane)
        {
            Point3d[] toSortArray = toSort.ToArray();
            List<Vector3d> vectors = new List<Vector3d>();
            double[] angles = new double[toSort.Count];

            //compute the reference vector for sorting
            Point3d startPoint;
            basePlane.RemapToPlaneSpace(new Point3d(1, 0, 0), out startPoint);
            Vector3d startVec = startPoint - basePlane.Origin;

            foreach (Point3d point in toSort)
            {
                vectors.Add(point - basePlane.Origin);
            }

            for (int i = 0; i < vectors.Count; i++)
            {
                angles[i] = Vector3d.VectorAngle(startVec, vectors[i], basePlane);
            }

            Array.Sort(angles, toSortArray);
            return toSortArray.ToList();
        }
        

        /// <summary>
        /// Construct both Brep and SubD Geometry of a Branch
        /// </summary>
        /// <param name="intersectionCorners"></param>
        /// <param name="radius"></param>
        /// <param name="basePlane"></param>
        /// <param name="Loft"></param>
        /// <param name="subD"></param>
        public static void LoftBranch(List<Point3d> intersectionCorners, double radius, Plane basePlane, out Brep Loft, out SubD subD)
        {
            //clean up and remove duplicate points before start
            intersectionCorners = Point3d.CullDuplicates(intersectionCorners, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance).ToList();
            //add the first point to the end to close the curve
            intersectionCorners.Add(intersectionCorners[0]);
            PolylineCurve intersectionCurve = new PolylineCurve(intersectionCorners);

            //construct the circle
            Circle circle = new Circle(basePlane, radius);
            NurbsCurve nurbsCircle = circle.ToNurbsCurve(3, intersectionCorners.Count - 1);

            //CreateSubDFriendly will cause issue that it adds too much control points
            //nurbsCircle = NurbsCurve.CreateSubDFriendly(nurbsCircle);

            //convert all the ControlPoints into Point3d and them add then into a list
            List<Point3d> nGonPoints = new List<Point3d>();
            foreach (ControlPoint cpoint in nurbsCircle.Points)
            {
                nGonPoints.Add(new Point3d(cpoint.X, cpoint.Y, cpoint.Z));
            }
            //remove all the duplicates of the points
            nGonPoints = Point3d.CullDuplicates(nGonPoints, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance).ToList();

            //reorder the points
            nGonPoints = PlaneRadialPointSort(nGonPoints, basePlane);
            //Add point to close the curve
            nGonPoints.Add(nGonPoints[0]);
            //construct the nGon on the basePlane
            PolylineCurve nGonOrdered = new PolylineCurve(nGonPoints);

            //determin the angle of the first intersection corner and the first nGon cornor on the basePlane
            double angleToRotate = Vector3d.VectorAngle(nGonPoints[0] - basePlane.Origin, intersectionCorners[0] - basePlane.Origin, basePlane);
            //rotate the nGon on the basePlane to align to the intersection polygon
            Transform rotation = Transform.Rotation(angleToRotate, basePlane.Normal, basePlane.Origin);
            nGonOrdered.Transform(rotation);


            //construct the geometry through Loft
            List<Curve> toLoft = new List<Curve> { nGonOrdered, intersectionCurve };
            //List<NurbsCurve> toLoftSubD = new List<NurbsCurve> { NurbsCurve.CreateSubDFriendly(nGonOrdered), NurbsCurve.CreateSubDFriendly(intersectionCurve) };
            //subD =  SubD.CreateFromLoft(toLoftSubD, false, true, false, 1);
            Loft = Brep.CreateFromLoft(toLoft, Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];
            Loft.Flip();
            //Extract all the faces from the loft Brep
            List<BrepFace> faces = new List<BrepFace>();
            for (int i = 0; i < Loft.Faces.Count; i++)
            {
                BrepFace singleFace = Loft.Faces[i];
                faces.Add(singleFace);
            }

            //create SubD faces from BrepFaces and add them into one open SubD object
            List<SubD> unjoined = new List<SubD>();
            foreach (BrepFace face in faces)
            {
                unjoined.Add(SubD.CreateFromSurface(face, SubDFromSurfaceMethods.FromNurbsControlNet, false));
            }

            subD = unjoined[0];
        }
        */
    }
}
