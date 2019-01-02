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

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Utility;
using Revit.IFC.Import.Geometry;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Class that represents IfcEdge entity
   /// </summary>
   public static class IFCEdge
   {
      /// <summary>
      /// Returns the curve which defines the shape and spatial location of this edge.
      /// </summary>
      /// <returns>The curve which defines the shape and spatial location of this edge.</returns>
      public static Curve GetGeometry(this IfcEdge edge, bool orientation)
      {
         IfcOrientedEdge orientedEdge = edge as IfcOrientedEdge;
         if (orientedEdge != null)
         {
            // TODO in REVIT-61368: get the correct orientation of the curve achieved for straight
            orientedEdge.EdgeElement.GetGeometry(orientedEdge.Orientation);
         }
         IfcVertexPoint start = edge.EdgeStart as IfcVertexPoint, end = edge.EdgeEnd as IfcVertexPoint;
         if (start == null || end == null)
         {
            Importer.TheLog.LogError(edge.StepId, "Invalid edge", true);
            return null;
         }
         if(orientation)
            return Line.CreateBound(start.VertexGeometry.ProcessIFCPoint(), end.VertexGeometry.ProcessIFCPoint());
         return Line.CreateBound(end.VertexGeometry.ProcessIFCPoint(), start.VertexGeometry.ProcessIFCPoint());
      }
   }
}