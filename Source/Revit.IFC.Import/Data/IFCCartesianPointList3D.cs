using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Class that represents an IfcCartesianPointList.
   /// </summary>
   /// <remarks>This can be either a IfcCartesianPointList2D or a IfcCartesianPoint3D.
   /// Both will be converted to XYZ values.</remarks>
   public static class IFCCartesianPointList
   {
      internal static IList<XYZ> GetPoints(this IfcCartesianPointList cartesianPointList)
      {
         IfcCartesianPointList3D cartesianPointList3D = cartesianPointList as IfcCartesianPointList3D;
         if (cartesianPointList3D != null)
            return cartesianPointList3D.CoordList.Select(x => new XYZ(IFCUnitUtil.ScaleLength(x[0]), IFCUnitUtil.ScaleLength(x[1]), IFCUnitUtil.ScaleLength(x[2]))).ToList();

         IfcCartesianPointList2D cartesianPointList2D = cartesianPointList as IfcCartesianPointList2D;
         if (cartesianPointList2D != null)
            return cartesianPointList2D.CoordList.Select(x => new XYZ(IFCUnitUtil.ScaleLength(x[0]), IFCUnitUtil.ScaleLength(x[1]), 0)).ToList();
         return null;
      }
   }
}