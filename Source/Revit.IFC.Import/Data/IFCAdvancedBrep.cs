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
   /// <summary>
   /// Class that represents IFCAdvancedBrep entity
   /// </summary>
   public static class IFCAdvancedBrep
   {
      /// <summary>
      /// Return geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The shape edit scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      /// <returns>The created geometry.</returns>
      internal static IList<GeometryObject> CreateGeometryAdvancedBrep(this IfcAdvancedBrep advancedBrep, CreateElementIfcCache cache,
         IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         // since IFCAdvancedBrep must contain a closed shell, we set the BuildPreferenceType to be solid for now
         IfcConnectedFaceSet outer = advancedBrep.Outer; 
         for (int pass = 0; pass < 2; pass++)
         {
            using (BuilderScope bs = shapeEditScope.InitializeBuilder(IFCShapeBuilderType.BrepBuilder))
            {
               BrepBuilderScope brepBuilderScope = bs as BrepBuilderScope;

               BRepType brepType = (pass == 0) ? BRepType.Solid : BRepType.OpenShell;
               brepBuilderScope.StartCollectingFaceSet(brepType);

               outer.CreateShapeConnectedFaceSet(cache, shapeEditScope, lcs, scaledLcs, guid, pass == 0);

               IList<GeometryObject> geomObjs = null;
               geomObjs = brepBuilderScope.CreateGeometry();

               // We'll return only if we have geometry; otherwise we'll try again with looser validation, if we can.
               if (geomObjs != null)
               {
                  if (pass == 1)
                     Importer.TheLog.LogError(advancedBrep.StepId, "Some faces are missing from this Solid; reading in as an Open Shell instead.", false);
                  return geomObjs;
               }
            }
         }

         return null;
      }
   }
}