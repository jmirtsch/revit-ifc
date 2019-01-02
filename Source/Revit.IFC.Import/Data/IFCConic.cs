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
   /// Class that represents IFCConic entity
   /// </summary>
   public static class IFCConic
   {
      public static Curve Curve(this IfcConic conic)
      {
         Transform position = conic.Position.GetAxis2PlacementTransform();
         IfcCircle circle = conic as IfcCircle;
         if(circle != null)
            return Arc.Create(position.Origin, IFCUnitUtil.ScaleLength(circle.Radius), 0, 2.0 * Math.PI, position.BasisX, position.BasisY);
         IfcEllipse ellipse = conic as IfcEllipse;
         if(ellipse != null)
            return Ellipse.CreateCurve(position.Origin, IFCUnitUtil.ScaleLength(ellipse.SemiAxis1), IFCUnitUtil.ScaleLength(ellipse.SemiAxis2), position.BasisX, position.BasisY, 0, 2.0 * Math.PI);

         return null;
      }
   }
}