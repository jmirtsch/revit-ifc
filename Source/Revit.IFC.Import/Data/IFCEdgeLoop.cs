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
   /// Class that represents IfcEdgeLoop entity
   /// </summary>
   public static class IFCEdgeLoop 
   {
         // TODO in REVIT-61368: check that edgeList is closed and continuous

      internal static CurveLoop GenerateLoop(this IfcEdgeLoop edgeLoop)
      {
         CurveLoop curveLoop = new CurveLoop();
         foreach (IfcOrientedEdge edge in edgeLoop.EdgeList)
         {
            if (edge != null)
               curveLoop.Append(edge.GetGeometry(true));
         }
         return curveLoop;
      }

      internal static bool CreateShapeEdgeLoop(this IfcEdgeLoop edgeLoop, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         if (cache.InvalidForCreation.Contains(edgeLoop.StepId))
            return false;
         if (shapeEditScope.BuilderType == IFCShapeBuilderType.BrepBuilder)
         {
            if (shapeEditScope.BuilderScope == null)
            {
               throw new InvalidOperationException("BuilderScope hasn't been initialized yet");
            }
            BrepBuilderScope brepBuilderScope = shapeEditScope.BuilderScope as BrepBuilderScope;

            if (brepBuilderScope == null)
            {
               throw new InvalidOperationException("The wrong BuilderScope is created");
            }

            foreach (IfcOrientedEdge edge in edgeLoop.EdgeList)
            {
               if (edge == null || edge.EdgeStart == null || edge.EdgeEnd == null)
               {
                  Importer.TheLog.LogError(edgeLoop.StepId, "Invalid edge loop", true);
                  return false;
               }

               edge.CreateShape(cache, shapeEditScope, lcs, scaledLcs, guid);

               if (lcs == null)
                  lcs = Transform.Identity;

               IfcEdge edgeElement = edge.EdgeElement;
               Curve edgeGeometry = null;
               if (edgeElement is IfcEdgeCurve)
               {
                  edgeGeometry = edgeElement.GetGeometry(edge.Orientation);
               }
               else
               {
                  //TODO: find a way to get the edgegeometry here
                  edgeGeometry = null;
               }

               if (edgeGeometry == null)
               {
                  Importer.TheLog.LogError(edgeElement.StepId, "Cannot get the edge geometry of this edge", true);
               }
               XYZ edgeStart = edgeElement.EdgeStart.GetPoint();
               XYZ edgeEnd = edgeElement.EdgeEnd.GetPoint();

               if (edgeStart == null || edgeEnd == null)
               {
                  Importer.TheLog.LogError(edgeLoop.StepId, "Invalid start or end vertices", true);
               }

               bool orientation = lcs.HasReflection ? !edge.Orientation : edge.Orientation;
               if (!brepBuilderScope.AddOrientedEdgeToTheBoundary(edgeElement.StepId, edgeGeometry.CreateTransformed(lcs), lcs.OfPoint(edgeStart), lcs.OfPoint(edgeEnd), edge.Orientation))
               {
                  Importer.TheLog.LogWarning(edge.StepId, "Cannot add this edge to the edge loop with Id: " + edgeLoop.StepId, false);
                  cache.InvalidForCreation.Add(edge.StepId);
                  return false;
               }
            }
         }
         else
         {
            Importer.TheLog.LogError(edgeLoop.StepId, "Unsupported IFCEdgeLoop", true);
         }
         return false;
      }
   }
}