using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace PrecisionNode
{
    public class PrecisionNodeInfo : GH_AssemblyInfo
    {
        public override string Name => "PrecisionNode";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "SubD-based node generator, similar result as Multipipe by Daniel Piker with precision for fabrication";

        public override Guid Id => new Guid("0A70CB12-94C8-4C22-8868-6FA7727F0D63");

        //Return a string identifying you or your company.
        public override string AuthorName => "Tao Sun";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "tao.sun@tum.de";
    }
}