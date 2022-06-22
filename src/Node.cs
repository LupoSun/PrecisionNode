using Rhino.Geometry;
using System.Collections.Generic;

namespace PrecisionNode
{
    public class Node
    {
        private List<NodeBranch> nodeBranches;
        private SubD nodeSimpleSubD;
        private readonly Brep coreGeometry;
        private readonly Brep coatingGeometry;

        public List<NodeBranch> NodeBranches => nodeBranches;
        public SubD NodeSimpleSubD => nodeSimpleSubD;

        /// <summary>
        /// Constructor for a empty Node object
        /// </summary>
        public Node()
        {
            this.nodeBranches = new List<NodeBranch>();
            this.nodeSimpleSubD = null;
            this.coreGeometry = null;
            this.coatingGeometry = null;
        }
        /// <summary>
        /// Construct a Node from a list of Nodebranches
        /// </summary>
        /// <param name="nodeBranches"></param>
        public Node(List<NodeBranch> nodeBranches)
        {
            this.nodeBranches = nodeBranches;
            this.nodeSimpleSubD = null;
            this.coreGeometry = null;
            this.coatingGeometry = null;
        }

        /// <summary>
        /// Create a simple SubD representation of the Node using the internal list of NodeBranches
        /// </summary>
        public void CreateNodeSimpleSubD()
        {
            List<SubD> branchSubDs = new List<SubD>();
            foreach (NodeBranch branch in nodeBranches)
            {
                branch.CreateSimpleSubD();
                branchSubDs.Add(branch.BranchSimpleSubD);
            }

            nodeSimpleSubD = SubD.JoinSubDs(branchSubDs, 0.01, false)[0];
        }

        public void CreateCoreGeometry(double wallThickness, double threadWallThickness, double threadLength)
        {

        }
    }
}
