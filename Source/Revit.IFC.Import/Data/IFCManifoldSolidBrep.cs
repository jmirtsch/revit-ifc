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
   public static class IFCManifoldSolidBrep
   {
      /// <summary>
      /// Return geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The shape edit scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      /// <returns>The created geometry.</returns>
      internal static IList<GeometryObject> CreateGeometryManifoldSolidBrep(this IfcManifoldSolidBrep manifoldSolidBrep, CreateElementIfcCache cache,
         IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IfcAdvancedBrep advancedBrep = manifoldSolidBrep as IfcAdvancedBrep;
         if (advancedBrep != null)
            return advancedBrep.CreateGeometryAdvancedBrep(cache, shapeEditScope, lcs, scaledLcs, guid);

         IfcClosedShell outer = manifoldSolidBrep.Outer;
         if (outer == null)
            return null;

         int faceCount = outer.CfsFaces.Count;
         if (faceCount == 0)
            return null;
         IList<GeometryObject> geomObjs = null;
         bool canRevertToMesh = false;

         using (BuilderScope bs = shapeEditScope.InitializeBuilder(IFCShapeBuilderType.TessellatedShapeBuilder))
         {
            TessellatedShapeBuilderScope tsBuilderScope = bs as TessellatedShapeBuilderScope;

            tsBuilderScope.StartCollectingFaceSet();
            outer.CreateShapeConnectedFaceSet(cache, shapeEditScope, lcs, scaledLcs, guid, false);

            if (tsBuilderScope.CreatedFacesCount == faceCount)
            {
               geomObjs = tsBuilderScope.CreateGeometry(guid);
            }

            canRevertToMesh = tsBuilderScope.CanRevertToMesh();
         }


         if (geomObjs == null || geomObjs.Count == 0)
         {
            if (canRevertToMesh)
            {
               using (IFCImportShapeEditScope.BuildPreferenceSetter setter =
                   new IFCImportShapeEditScope.BuildPreferenceSetter(shapeEditScope, IFCImportShapeEditScope.BuildPreferenceType.AnyMesh))
               {
                  using (BuilderScope newBuilderScope = shapeEditScope.InitializeBuilder(IFCShapeBuilderType.TessellatedShapeBuilder))
                  {
                     TessellatedShapeBuilderScope newTsBuilderScope = newBuilderScope as TessellatedShapeBuilderScope;
                     // Let's see if we can loosen the requirements a bit, and try again.
                     newTsBuilderScope.StartCollectingFaceSet();

                     outer.CreateShapeConnectedFaceSet(cache, shapeEditScope, lcs, scaledLcs, guid, false);

                     // This needs to be in scope so that we keep the mesh tolerance for vertices.
                     if (newTsBuilderScope.CreatedFacesCount != 0)
                     {
                        if (newTsBuilderScope.CreatedFacesCount != faceCount)
                           Importer.TheLog.LogWarning
                               (outer.StepId, "Processing " + newTsBuilderScope.CreatedFacesCount + " valid faces out of " + faceCount + " total.", false);

                        geomObjs = newTsBuilderScope.CreateGeometry(guid);
                     }

                  }
               }
            }
         }

         if (geomObjs == null || geomObjs.Count == 0)
         {
            // Couldn't use fallback, or fallback didn't work.
            Importer.TheLog.LogWarning(manifoldSolidBrep.StepId, "Couldn't create any geometry.", false);
            return null;
         }

         return geomObjs;
      }

      

      /// <summary>
      /// Create geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      internal static void CreateShapeManifoldSolidBrep(this IfcManifoldSolidBrep manifoldSolidBrep, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         // Ignoring Inner shells for now.
         IfcConnectedFaceSet outer = manifoldSolidBrep.Outer;
         if (outer == null)
            return;

         IList<GeometryObject> geometry = new List<GeometryObject>();

         IfcAdvancedBrep advancedBrep = manifoldSolidBrep as IfcAdvancedBrep;
         if (advancedBrep != null)
         {
            geometry = advancedBrep.CreateGeometryAdvancedBrep(cache, shapeEditScope, lcs, scaledLcs, guid);
         }
         else
         {
            geometry = manifoldSolidBrep.CreateGeometryManifoldSolidBrep(cache, shapeEditScope, lcs, scaledLcs, guid);
         }
         if (geometry != null)
         {
            foreach (GeometryObject geom in geometry)
            {
               shapeEditScope.Solids.Add(IFCSolidInfo.Create(manifoldSolidBrep.StepId, geom));
            }
         }
      }
   }
}