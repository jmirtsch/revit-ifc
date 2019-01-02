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
   public static class IFCShellBasedSurfaceModel
   {
      private static void WarnOnTooFewCreatedFaces(this IfcShellBasedSurfaceModel shellBasedSurfaceModel, IList<GeometryObject> geomObjs, int numExpectedFaces)
      {
         if (geomObjs == null)
            return;

         int numCreatedFaces = 0;

         foreach (GeometryObject geomObj in geomObjs)
         {
            if (geomObj is Solid)
               numCreatedFaces += (geomObj as Solid).Faces.Size;
            else if (geomObj is Mesh)
               numCreatedFaces += (geomObj as Mesh).NumTriangles;
            else
               return;   // We don't know what this is, and can't count it.
         }

         // Note that if we created a Mesh, the number of created faces can be larger than the number of expected faces, which may have been polygons with more than 3 sides.
         // As such, this warning can't guarantee it will always complain.
         if (numCreatedFaces < numExpectedFaces)
         {
            Importer.TheLog.LogWarning
                (shellBasedSurfaceModel.StepId, "Created " + numCreatedFaces + " valid faces out of " + numExpectedFaces + " total.  This may be due to slivery triangles or other similar geometric issues.", false);
         }
      }

      /// <summary>
      /// Return geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The shape edit scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      /// <returns>The created geometry.</returns>
      /// <remarks>As this doesn't inherit from IfcSolidModel, this is a non-virtual CreateSolid function.</remarks>
      internal static IList<GeometryObject> CreateGeometryShellBasedSurfaceModel(this IfcShellBasedSurfaceModel shellBasedSurfaceModel, CreateElementIfcCache cache,
            IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IList<IfcShell> shells = shellBasedSurfaceModel.SbsmBoundary;
         if (shells.Count == 0)
            return null;

         IList<GeometryObject> geomObjs = null;
         int numExpectedFaces = 0;
         foreach (IfcConnectedFaceSet faceSet in shells)
         {
            numExpectedFaces += faceSet.CfsFaces.Count;
         }

         // We are going to start by trying to create a Solid, even if we are passed a shell-based model, since we can frequently
         // do so.  However, if we have even one missing face, we'll loosen the requirements and revert to mesh only.
         for (int pass = 0; pass < 2; pass++)
         {
            IFCImportShapeEditScope.BuildPreferenceType target =
               (pass == 0) ? IFCImportShapeEditScope.BuildPreferenceType.AnyGeometry : IFCImportShapeEditScope.BuildPreferenceType.AnyMesh;
            using (IFCImportShapeEditScope.BuildPreferenceSetter setter =
               new IFCImportShapeEditScope.BuildPreferenceSetter(shapeEditScope, target))
            {
               using (BuilderScope bs = shapeEditScope.InitializeBuilder(IFCShapeBuilderType.TessellatedShapeBuilder))
               {
                  TessellatedShapeBuilderScope tsBuilderScope = shapeEditScope.BuilderScope as TessellatedShapeBuilderScope;

                  tsBuilderScope.StartCollectingFaceSet();

                  foreach (IfcConnectedFaceSet faceSet in shells)
                  {
                     faceSet.CreateShapeConnectedFaceSet(cache, shapeEditScope, lcs, scaledLcs, guid, pass == 0);
                  }

                  // If we are on our first pass, try again.  If we are on our second pass, warn and create the best geometry we can.
                  if (tsBuilderScope.CreatedFacesCount != numExpectedFaces)
                  {
                     if (pass == 0)
                        continue;

                     Importer.TheLog.LogWarning (shellBasedSurfaceModel.StepId, "Processing " + tsBuilderScope.CreatedFacesCount + " valid faces out of " + numExpectedFaces + " total.", false);
                  }

                  geomObjs = tsBuilderScope.CreateGeometry(guid);

                  shellBasedSurfaceModel.WarnOnTooFewCreatedFaces(geomObjs, numExpectedFaces);

                  break;
               }
            }
         }

         if (geomObjs == null || geomObjs.Count == 0)
         {
            if (numExpectedFaces != 0)
            {
               Importer.TheLog.LogError
                   (shellBasedSurfaceModel.StepId, "No valid geometry found.  This may be due to slivery triangles or other similar geometric issues.", false);
               return null;
            }
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
      internal static void CreateShapeShellBasedSurfaceModel(this IfcShellBasedSurfaceModel shellBasedSurfaceModel, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IList<GeometryObject> geometry = shellBasedSurfaceModel.CreateGeometryShellBasedSurfaceModel(cache, shapeEditScope, lcs, scaledLcs, guid);
         if (geometry != null)
         {
            foreach (GeometryObject geom in geometry)
            {
               shapeEditScope.Solids.Add(IFCSolidInfo.Create(shellBasedSurfaceModel.StepId, geom));
            }
         }
      }
   }
}