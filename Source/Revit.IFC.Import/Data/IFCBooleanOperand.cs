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
   static class IFCBooleanOperand
   {
      public static IList<GeometryObject> CreateGeometryBooleanOperand(this IfcBooleanOperand booleanOperand, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         if (booleanOperand == null)
         {
            //LOG: ERROR: IfcSolidModel is null or has no value
            return null;
         }

         IfcBooleanResult booleanResult = booleanOperand as IfcBooleanResult;
         if (booleanResult != null)
            return booleanResult.CreateGeometryBooleanResult(cache, shapeEditScope, lcs, scaledLcs, guid);
         {
            IfcHalfSpaceSolid halfSpaceSolid = booleanOperand as IfcHalfSpaceSolid;
            if (halfSpaceSolid != null)
               return halfSpaceSolid.CreateGeometryHalfSpacedSolid(cache, shapeEditScope, lcs, scaledLcs, guid);
            else
            {
               IfcSolidModel solidModel = booleanOperand as IfcSolidModel;
               if (solidModel != null)
                  return solidModel.CreateGeometrySolidModel(cache, shapeEditScope, lcs, scaledLcs, guid);
            }

            Importer.TheLog.LogUnhandledSubTypeError(booleanOperand as BaseClassIfc, "IfcBooleanOperand", true);
            return null;
         }
      }

      /// <summary>
      /// In case of a Boolean operation failure, provide a recommended direction to shift the geometry in for a second attempt.
      /// </summary>
      /// <param name="lcs">The local transform for this entity.</param>
      /// <returns>An XYZ representing a unit direction vector, or null if no direction is suggested.</returns>
      /// <remarks>If the 2nd attempt fails, a third attempt will be done with a shift in the opposite direction.</remarks>
      public static XYZ GetSuggestedShiftDirection(this IfcBooleanOperand booleanOperand, Transform lcs)
      {
         IfcHalfSpaceSolid halfSpaceSolid = booleanOperand as IfcHalfSpaceSolid;
         if(halfSpaceSolid != null)
         {
            IfcPlane ifcPlane = halfSpaceSolid.BaseSurface as IfcPlane;
            Plane plane = (ifcPlane != null) ? ifcPlane.Plane() : null;
            XYZ untransformedNorm = (plane != null) ? plane.Normal : null;
            return (lcs == null) ? untransformedNorm : lcs.OfVector(untransformedNorm);
         }
         return null;
      }
   }
}