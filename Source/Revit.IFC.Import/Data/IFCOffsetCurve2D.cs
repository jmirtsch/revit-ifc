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
   public static class IFCOffsetCurve2D
   {
      public static Curve Curve(this IfcOffsetCurve2D offsetCurve2D)
      {
         IfcCurve basisCurve = offsetCurve2D.BasisCurve;
         double distance = IFCUnitUtil.ScaleLength(offsetCurve2D.Distance);
         if (double.IsNaN(distance))
            distance = 0.0;

         Curve curve = basisCurve.Curve();
         XYZ dirXYZ = basisCurve.GetNormal();

         try
         {
            if (curve != null)
               return curve.CreateOffset(distance, XYZ.BasisZ);
         }
         catch
         {
            Importer.TheLog.LogError(offsetCurve2D.StepId, "Couldn't create offset curve.", false);
         }
         return null;
      }

      public static CurveLoop CurveLoop(this IfcOffsetCurve2D offsetCurve2D)
      {
         IfcCurve basisCurve = offsetCurve2D.BasisCurve;
         double distance = IFCUnitUtil.ScaleLength(offsetCurve2D.Distance);
         if (double.IsNaN(distance))
            distance = 0.0;

         CurveLoop curve = basisCurve.GetCurveLoop();
         XYZ dirXYZ = basisCurve.GetNormal();

         try
         {
            CurveLoop curveLoop = basisCurve.CurveLoop();
            if (curveLoop != null)
            {
               return Autodesk.Revit.DB.CurveLoop.CreateViaOffset(curveLoop, distance, XYZ.BasisZ);

            }
         }
         catch
         {
            Importer.TheLog.LogError(offsetCurve2D.StepId, "Couldn't create offset Curve Looop.", false);
         }
         return null;
      }
   }
}