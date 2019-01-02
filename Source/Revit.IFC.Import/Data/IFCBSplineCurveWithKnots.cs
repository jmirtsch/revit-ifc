//
// Revit IFC Import library: this library works with Autodesk(R) Revit(R) to import IFC files.
// Copyright (C) 2013  Autodesk, Inc.
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Geometry;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public static class IFCBSplineCurveWithKnots
   {
      public static Curve BSplineCurve(this IfcBSplineCurveWithKnots ifcCurve)
      {
         IList<int> knotMultiplicities = ifcCurve.KnotMultiplicities;
         IList<double> knots = ifcCurve.Knots;

         if (knotMultiplicities == null || knots == null)
         {
            Importer.TheLog.LogError(ifcCurve.StepId, "Cannot find the KnotMultiplicities or Knots attribute of this IfcBSplineCurveWithKnots", true);
         }

         if (knotMultiplicities.Count != knots.Count)
         {
            Importer.TheLog.LogError(ifcCurve.StepId, "The number of knots and knot multiplicities are not the same", true);
         }

         IList<double> revitKnots = IFCGeometryUtil.ConvertIFCKnotsToRevitKnots(knotMultiplicities, knots);

         Curve curve = NurbSpline.CreateCurve(ifcCurve.Degree, revitKnots, IFCPoint.ProcessScaledLengthIFCCartesianPoints(ifcCurve.ControlPointsList));

         if (curve == null)
         {
            Importer.TheLog.LogWarning(ifcCurve.StepId, "Cannot get the curve representation of this IfcCurve", false);
         }

         return curve;
      }

      internal static bool constraintsParamBSpline(this IfcBSplineCurveWithKnots bSplineCurveWithKnots)
      {
         // TODO: implement this function to validate NURBS data
         //       implementation can be found here http://www.buildingsmart-tech.org/ifc/IFC4/final/html/schema/ifcgeometryresource/lexical/ifcconstraintsparambspline.htm
         //       move this function to the correct place
         return true;
      }
   }
}