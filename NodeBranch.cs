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
        public int BranchNum { get { return branchNum; } }

        public Curve CylinderIntersection { get { return cylinderIntersection; } }

        /// <summary>
        /// Automatically add more intersectionCorners to the internal list
        /// </summary>
        /// <param name="cornerToAdd"></param>
        public void AddIntersectionCorners(Point3d cornerToAdd)
        {
            List<Point3d> cornersToAdd = new List<Point3d>{cornerToAdd};
            intersectionCorners = GetIntersectionCorners(cylinderIntersection, branchStartPlane,40, cornersToAdd);
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
        
    }
}
