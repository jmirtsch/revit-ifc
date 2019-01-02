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
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcRoot object.
   /// </summary>
   public static class IFCRoot
   {
      /// <summary>
      /// True if two IFCRoots have the same id, or are both null.
      /// </summary>
      /// <param name="first">The first IFCRoot.</param>
      /// <param name="second">The second IFCRoot.</param>
      /// <returns>True if two IFCRoots have the same id, or are both null, or false otherwise.</returns>
      public static bool Equals(IBaseClassIfc first, IBaseClassIfc second)
      {
         if (first == null)
         {
            if (second != null) return false;
         }
         else if (second == null)
         {
            return false;   // first != null, otherwise we are in first case above.
         }
         else
         {
            if (first.StepId != second.StepId) return false;
         }

         return true;
      }

      /// <summary>
      /// Determines if we require the IfcRoot entity to have a name.
      /// </summary>
      /// <returns>Returns true if we require the IfcRoot entity to have a name.</returns>
      internal static bool CreateNameIfNull(this IfcRoot root)
      {
         if (root is IfcPropertySetDefinition)
            return true;
         return false;
      }
   }
}