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
using Autodesk.Revit.ApplicationServices;
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
   public static class IFCFaceBound
   {
      public static bool IsOuter(this IfcFaceBound faceBound)
      {
         return faceBound is IfcFaceOuterBound;
      }

      private static IList<XYZ> GetLoopVertices(this IfcFaceBound faceBound)
      {
         IfcLoop loop = faceBound.Bound;
         IfcPolyloop polyloop = loop as IfcPolyloop;
         if (polyloop != null)
            return polyloop.Vertex();

         return null;
      }
      private static bool CreateTessellatedShapeInternal(this IfcFaceBound faceBound, IFCImportShapeEditScope shapeEditScope, Transform scaledLcs)
      {
         TessellatedShapeBuilderScope tsBuilderScope = shapeEditScope.BuilderScope as TessellatedShapeBuilderScope;

         if (tsBuilderScope == null)
         {
            throw new InvalidOperationException("Expect a TessellatedShapeBuilderScope, but get a BrepBuilderScope instead");
         }

         IList<XYZ> loopVertices = faceBound.GetLoopVertices();
         int count = 0;
         if (loopVertices == null || ((count = loopVertices.Count) == 0))
            throw new InvalidOperationException("#" + faceBound.StepId + ": missing loop vertices, ignoring.");

         if (count < 3)
            throw new InvalidOperationException("#" + faceBound.StepId + ": too few loop vertices (" + count + "), ignoring.");


         if (!faceBound.Orientation)
            loopVertices.Reverse();

         // Apply the transform
         IList<XYZ> transformedVertices = new List<XYZ>();
         foreach (XYZ vertex in loopVertices)
         {
            transformedVertices.Add(scaledLcs.OfPoint(vertex));
         }

         // Check that the loop vertices don't contain points that are very close to one another;
         // if so, throw the point away and hope that the TessellatedShapeBuilder can repair the result.
         // Warn in this case.  If the entire boundary is bad, report an error and don't add the loop vertices.

         IList<XYZ> validVertices;
         IFCGeometryUtil.CheckAnyDistanceVerticesWithinTolerance(faceBound.StepId, shapeEditScope, transformedVertices, out validVertices);

         // We are going to catch any exceptions if the loop is invalid.  
         // We are going to hope that we can heal the parent object in the TessellatedShapeBuilder.
         bool bPotentiallyAbortFace = false;

         count = validVertices.Count;
         if (count < 3)
         {
            Importer.TheLog.LogComment(faceBound.StepId, "Too few distinct loop vertices (" + count + "), ignoring.", false);
            bPotentiallyAbortFace = true;
         }
         else
         {
            // Last check: check to see if the vertices are actually planar.  If not, for the vertices to be planar.
            // We are not going to be particularly fancy about how we pick the plane.
            XYZ planeNormal = null;
            bool foundNormal = false;

            XYZ firstPoint = validVertices[0];
            XYZ secondPoint = validVertices[1];
            XYZ firstDir = secondPoint - firstPoint;

            int thirdPointIndex = 2;
            for (; thirdPointIndex < count; thirdPointIndex++)
            {
               XYZ thirdPoint = validVertices[thirdPointIndex];
               planeNormal = firstDir.CrossProduct(thirdPoint - firstPoint);
               if (!planeNormal.IsZeroLength())
               {
                  planeNormal = planeNormal.Normalize();
                  foundNormal = true;
                  break;
               }
            }

            if (!foundNormal)
            {
               Importer.TheLog.LogComment(faceBound.StepId, "Loop is degenerate, ignoring.", false);
               bPotentiallyAbortFace = true;
            }
            else
            {
               double vertexEps = IFCImportFile.TheFile.Document.Application.VertexTolerance;

               for (++thirdPointIndex; thirdPointIndex < count; thirdPointIndex++)
               {
                  XYZ pointOnPlane = validVertices[thirdPointIndex] -
                     (validVertices[thirdPointIndex] - firstPoint).DotProduct(planeNormal) * planeNormal;
                  if (pointOnPlane.DistanceTo(validVertices[thirdPointIndex]) > vertexEps)
                  {
                     Importer.TheLog.LogComment(faceBound.StepId, "Bounded loop plane is slightly non-planar, correcting.", false);
                     validVertices[thirdPointIndex] = pointOnPlane;
                  }
               }

               if (!tsBuilderScope.AddLoopVertices(faceBound.StepId, validVertices))
                  bPotentiallyAbortFace = true;
            }
         }

         if (bPotentiallyAbortFace && faceBound.IsOuter())
         {
            tsBuilderScope.AbortCurrentFace();
            return false;
         }
         return true;
      }

      /// <summary>
      /// Create geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      internal static bool CreateShapeFaceBound(this IfcFaceBound faceBound, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         if (shapeEditScope.BuilderScope == null)
         {
            throw new InvalidOperationException("BuilderScope has not been initialised");
         }

         //faceBound.Bound.CreateShape(cache, shapeEditScope, lcs, scaledLcs, guid);
         if (faceBound.Bound.CreateShapeLoop(cache, shapeEditScope, lcs, scaledLcs, guid))
            return true;

         if (shapeEditScope.BuilderType == IFCShapeBuilderType.TessellatedShapeBuilder)
            return faceBound.CreateTessellatedShapeInternal(shapeEditScope, scaledLcs);

         return false;
      }
   } 
}