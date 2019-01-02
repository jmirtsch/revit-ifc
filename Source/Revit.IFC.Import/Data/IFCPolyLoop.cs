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
   public static class IFCPolyLoop
   {
      public static IList<XYZ> Vertex(this IfcPolyloop polyLoop)
      {
         IList<IfcCartesianPoint> polygon = polyLoop.Polygon;

         if (polygon == null)
            return null; // TODO: WARN

         IList<XYZ> points = IFCPoint.ProcessScaledLengthIFCCartesianPoints(polygon);

         int numVertices = points.Count;
         if (numVertices > 1)
         {
            if (points[0].IsAlmostEqualTo(points[numVertices - 1]))
            {
               // LOG: Warning: #: First and last points are almost identical, removing extra point.
               points.RemoveAt(numVertices - 1);
               numVertices--;
            }
         }

         if (numVertices < 3)
            throw new InvalidOperationException("#" + polyLoop.StepId + ": Polygon attribute has only " + numVertices + " vertices, 3 expected.");
         return points;
      }
      public static CurveLoop GetCurveLoop(this IfcPolyloop polyLoop)
      {
         IList<XYZ> points = polyLoop.Vertex();
         if (points == null)
            return null;
         return IFCGeometryUtil.CreatePolyCurveLoop(points, null, polyLoop.StepId, true);

      }
   }
}