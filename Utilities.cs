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
    }
}
