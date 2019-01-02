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
   public static class IFCSolidModel
   {
      internal static IList<GeometryObject> CreateGeometrySolidModelInternal(this IfcSolidModel solidModel, CreateElementIfcCache cache,
         IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IfcCsgSolid csgSolid = solidModel as IfcCsgSolid;
         if (csgSolid != null)
            return csgSolid.CreateGeometryCsgSolid(cache, shapeEditScope, lcs, scaledLcs, guid);
         IfcManifoldSolidBrep manifoldSolidBrep = solidModel as IfcManifoldSolidBrep;
         if (manifoldSolidBrep != null)
            return manifoldSolidBrep.CreateGeometryManifoldSolidBrep(cache, shapeEditScope, lcs, scaledLcs, guid);
         IfcSweptAreaSolid sweptAreaSolid = solidModel as IfcSweptAreaSolid;
         if(sweptAreaSolid != null)
         {
            IfcExtrudedAreaSolid extrudedAreaSolid = sweptAreaSolid as IfcExtrudedAreaSolid;
            if (extrudedAreaSolid != null)
               return extrudedAreaSolid.CreateGeometryExtrudedAreaSolid(cache, shapeEditScope, lcs, scaledLcs, guid);
            IfcRevolvedAreaSolid revolvedAreaSolid = sweptAreaSolid as IfcRevolvedAreaSolid;
            if (revolvedAreaSolid != null)
               return revolvedAreaSolid.CreateGeometryRevolvedAreaSolid(cache, shapeEditScope, lcs, scaledLcs, guid);
            IfcSurfaceCurveSweptAreaSolid surfaceCurveSweptAreaSolid = sweptAreaSolid as IfcSurfaceCurveSweptAreaSolid;
            if (surfaceCurveSweptAreaSolid != null)
               return surfaceCurveSweptAreaSolid.CreateGeometrySurfaceCurveSweptAreaSolid(cache, shapeEditScope, lcs, scaledLcs, guid);
         }
         IfcSweptDiskSolid sweptDiskSolid = solidModel as IfcSweptDiskSolid;
         if(sweptDiskSolid != null)
            return sweptDiskSolid.CreateGeometrySweptDiskSolid(cache, shapeEditScope, lcs, scaledLcs, guid);

         Importer.TheLog.LogUnhandledSubTypeError(solidModel, "IfcAxis2Placement", false);
         return null;
      }

      /// <summary>
      /// Return geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The shape edit scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      /// <returns>Zero or more created geometries.</returns>
      public static IList<GeometryObject> CreateGeometrySolidModel(this IfcSolidModel solidModel, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IfcStyledItem styledItem = solidModel.StyledByItem;
         if (styledItem != null)
            styledItem.Create(shapeEditScope, cache);

         using (IFCImportShapeEditScope.IFCMaterialStack stack = new IFCImportShapeEditScope.IFCMaterialStack(shapeEditScope, cache, styledItem, null))
         {
            return solidModel.CreateGeometrySolidModelInternal(cache, shapeEditScope, lcs, scaledLcs, guid);
         }
      }
      /// <summary>
      /// Create geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which representation is being created.</param>
      internal static void CreateShapeSolidModel(this IfcSolidModel solidModel, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IList<GeometryObject> solidGeometries = solidModel.CreateGeometrySolidModel(cache, shapeEditScope, lcs, scaledLcs, guid);
         if (solidGeometries != null)
         {
            foreach (GeometryObject geometry in solidGeometries)
            {
               shapeEditScope.Solids.Add(IFCSolidInfo.Create(solidModel.StepId, geometry));
            }
         }
      }
   }
}