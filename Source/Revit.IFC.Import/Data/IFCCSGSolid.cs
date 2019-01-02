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
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public static class IFCCSGSolid
   {
      internal static IList<GeometryObject> CreateGeometryCsgSolid(this IfcCsgSolid csgSolid, CreateElementIfcCache cache,
         IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IfcBooleanResult booleanResult = csgSolid.TreeRootExpression as IfcBooleanResult;
         if (booleanResult != null)
            return booleanResult.CreateGeometryBooleanResult(cache, shapeEditScope, lcs, scaledLcs, guid);
         return null;
      }

      /// <summary>
      /// Create geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      internal static void CreateShapeCsgSolid(this IfcCsgSolid csgSolid, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IList<GeometryObject> csgGeometries = csgSolid.CreateGeometryCsgSolid(cache, shapeEditScope, lcs, scaledLcs, guid);
         if (csgGeometries != null)
         {
            foreach (GeometryObject csgGeometry in csgGeometries)
            {
               shapeEditScope.Solids.Add(IFCSolidInfo.Create(csgSolid.StepId, csgGeometry));
            }
         }
      }


      
   }
}