using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino;
using Rhino.Geometry;

namespace PrecisionNode
{
    public class Utilities
    {
        public Utilities() { }

        /// <summary>
        /// Find the centre point and the further reach points from a list of pipe lines
        /// </summary>
        /// <param name="pipeLines">a list of pipe centre lines</param>
        /// <param name="branchStartPoints">declared list of the further reach points</param>
        /// <returns></returns>
        public static Point3d PipeLinesSort(List<Curve> pipeLines, out List<Point3d> branchStartPoints)
        {
            branchStartPoints = new List<Point3d>();

            List<Point3d> endPoints1 = new List<Point3d> { pipeLines[0].PointAtEnd, pipeLines[0].PointAtStart };
            List<Point3d> endPoints2 = new List<Point3d> { pipeLines[1].PointAtEnd, pipeLines[1].PointAtStart };
            Point3d centrePoint = new Point3d();
            foreach (Point3d point1 in endPoints1)
            {
                foreach (Point3d point2 in endPoints2)
                {
                    if (point1.DistanceTo(point2) < 0.01) centrePoint = point1;
                }
            }

            foreach (Curve curve in pipeLines)
            {
                if (curve.PointAtEnd.DistanceTo(centrePoint) < 0.01) branchStartPoints.Add(curve.PointAtStart);
                else branchStartPoints.Add(curve.PointAtEnd);
            }


            return centrePoint;
        }

        /// <summary>
        /// Find the intersection curve between one branch and the other ones
        /// </summary>
        /// <param name="startPoint">The start point of a branch</param>
        /// <param name="branchStartPoints">All the branchStartPoints of a Node</param>
        /// <param name="centrePoint">The centre point of a Node</param>
        /// <param name="radius">The radius of the branch</param>
        /// <param name="intersection">The intersection curve</param>
        /// <returns>true if the operation is successful, false if it failed</returns>
        public static bool CylinderIntersection(Point3d startPoint, List<Point3d> branchStartPoints, Point3d centrePoint, double radius, out Curve intersection)
        {
            //make a copy of the original branchStartPoints list
            List<Point3d> startPoints = new List<Point3d>(branchStartPoints);
            //set impossible integer as default index
            int removalIndex = 1000;
            intersection = null;
            for (int i = 0; i < startPoints.Count; i++)
            {
                if (startPoint.DistanceTo(startPoints[i]) < 0.001)
                {
                    removalIndex = i;
                }
            }
            //check if the operation succeded
            if (removalIndex != 1000)
            {
                startPoints.RemoveAt(removalIndex);
                List<Brep> otherCylinders = new List<Brep>();
                //add all cylinders into one list
                foreach (Point3d point in startPoints)
                {
                    foreach (Brep brep in Brep.CreatePipe(new LineCurve(point, centrePoint),
                      radius, false,
                      PipeCapMode.None,
                      true,
                      RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                      RhinoDoc.ActiveDoc.ModelAngleToleranceRadians))
                    {
                        otherCylinders.Add(brep);
                    }
                }
                //join all the cylinder into one Brep using Boolean-Union operation
                Brep joinedCylinders = Brep.CreateBooleanUnion(otherCylinders, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)[0];

                //construct the branchCylinder
                Brep branchCylinder = Brep.CreatePipe(new LineCurve(startPoint, centrePoint),
                  radius,
                  false,
                  PipeCapMode.None,
                  true,
                  RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                  RhinoDoc.ActiveDoc.ModelAngleToleranceRadians)[0];

                //preparation for Brep-Brep intersection
                Curve[] intersectionCurves;
                Point3d[] intersectionPoints;
                if (Rhino.Geometry.Intersect.Intersection.BrepBrep(branchCylinder,
                        joinedCylinders,
                        RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                        out intersectionCurves,
                        out intersectionPoints))
                {
                    intersection = intersectionCurves[0];
                    return true;
                }
            }
            return false;

        }

        /// <summary>
        /// By having the PolyCurve of the cylinder intersection, this function rebuilds the curve into
        /// PolyLineCurves according the the division ANGLE and retrieve the corner points
        /// </summary>
        /// <param name="cylinderIntersection"></param>
        /// <param name="basePlane"></param>
        /// <param name="divisionAngle"></param>
        /// <param name="averagePosition"></param>
        /// <returns></returns>
        public static List<Point3d> GetIntersectionCorners(Curve cylinderIntersection, Plane basePlane, double divisionAngle = 40, List<Point3d> averagePosition = null)
        {
            List<Curve> segments = new List<Curve>();
            Curve[] curveSegments;
            if (cylinderIntersection is PolyCurve)
            {
                //check if the intersection is a PolyCurve 
                curveSegments = ((PolyCurve)cylinderIntersection).Explode();    
            }
            else { curveSegments = new Curve[] { cylinderIntersection }; }
            segments.AddRange(curveSegments);

            if ((averagePosition != null) && (!cylinderIntersection.IsClosed))
            {
                segments.Add(new LineCurve(cylinderIntersection.PointAtEnd, averagePosition[0]));
                segments.Add(new LineCurve(averagePosition[0], cylinderIntersection.PointAtStart));

            }
            
            
            List<NurbsCurve> rebuiltSegments = new List<NurbsCurve>();
            List<Point3d> intersectionCorners = new List<Point3d>();

            Transform projection = Transform.PlanarProjection(basePlane);

            foreach (Curve curve in segments)
            {
                //measure the angle between two vectors from the endpoints to the plane origin with respect of normal vector
                Vector3d vStart = curve.PointAtStart - basePlane.Origin;
                vStart.Transform(projection);
                Vector3d vEnd = curve.PointAtEnd - basePlane.Origin;
                vEnd.Transform(projection);
                int segmentNum = (int)(Vector3d.VectorAngle(vStart, vEnd) / (Math.PI / 180 * (divisionAngle - RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)));
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
        /// By having the PolyCurve of the cylinder intersection, this function rebuilds the curve into
        /// PolyLineCurves according the the division LENGTH and retrieve the corner points
        /// </summary>
        /// <param name="cylinderIntersection"></param>
        /// <param name="basePlane"></param>
        /// <param name="averagePosition"></param>
        /// <param name="divisionLength"></param>
        /// <returns></returns>
        public static List<Point3d> GetIntersectionCorners(Curve cylinderIntersection, double divisionLength, Plane basePlane, List<Point3d> averagePosition = null)
        {
            List<Curve> segments = new List<Curve>();
            Curve[] curveSegments;
            if (cylinderIntersection is PolyCurve)
            {
                //check if the intersection is a PolyCurve 
                curveSegments = ((PolyCurve)cylinderIntersection).Explode();
            }
            else { curveSegments = new Curve[] { cylinderIntersection }; }
            segments.AddRange(curveSegments);

            if ((averagePosition != null) && (!cylinderIntersection.IsClosed))
            {
                segments.Add(new LineCurve(cylinderIntersection.PointAtEnd, averagePosition[0]));
                segments.Add(new LineCurve(averagePosition[0], cylinderIntersection.PointAtStart));

            }


            List<NurbsCurve> rebuiltSegments = new List<NurbsCurve>();
            List<Point3d> intersectionCorners = new List<Point3d>();

            Transform projection = Transform.PlanarProjection(basePlane);

            foreach (Curve curve in segments)
            {
                //measure the length of the segment
                double length = curve.GetLength();
                int segmentNum = (int)(length / divisionLength);
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
            //Sort intersectionCorners Radially
            intersectionCorners = PlaneRadialPointSort(intersectionCorners, basePlane);
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
    }
}
