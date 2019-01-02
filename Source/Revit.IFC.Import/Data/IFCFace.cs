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
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Utility;
using Revit.IFC.Import.Enums;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public static class IFCFace
   {
      /// <summary>
      /// Create geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      internal static bool CreateShapeFace(this IfcFace face, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IfcAdvancedFace advancedFace = face as IfcAdvancedFace;
         if(advancedFace != null)
         {
            return advancedFace.CreateShapeAdvancedFace(cache, shapeEditScope, lcs, scaledLcs, guid);
         }
         if (shapeEditScope.BuilderType != IFCShapeBuilderType.TessellatedShapeBuilder)
            throw new InvalidOperationException("Currently BrepBuilder is only used to support IFCAdvancedFace");

         // we would only be in this code if we are not processing and IfcAdvancedBrep, since IfcAdvancedBrep must have IfcAdvancedFace
         if (shapeEditScope.BuilderScope == null)
         {
            throw new InvalidOperationException("BuilderScope has not been initialized");
         }
         TessellatedShapeBuilderScope tsBuilderScope = shapeEditScope.BuilderScope as TessellatedShapeBuilderScope;

         tsBuilderScope.StartCollectingFace(face.GetMaterialElementId(cache, shapeEditScope));
         foreach (IfcFaceBound faceBound in face.Bounds)
         {
            faceBound.CreateShapeFaceBound(cache,shapeEditScope, lcs, scaledLcs, guid);

            // If we can't create the outer face boundary, we will abort the creation of this face.  In that case, return.
            if (!tsBuilderScope.HaveActiveFace())
            {
               tsBuilderScope.AbortCurrentFace();
               return false;
            }
         }

         tsBuilderScope.StopCollectingFace();
         return true;
      }
   }
}