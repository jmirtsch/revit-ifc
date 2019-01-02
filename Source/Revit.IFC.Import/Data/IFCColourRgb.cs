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
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public static class IFCColourRgb 
   {
      public static Color DefaultColor() { return new Color(127, 127, 127); }
      /// <summary>
      /// Get the RGB associated to the color.
      /// </summary>
      /// <returns>The Color value.</returns>
      public static Color CreateColor(this IfcColourRgb colourRgb)
      {
         if(colourRgb == null)
            return DefaultColor();
         byte red = (byte)(colourRgb.Red * 255 + 0.5);
         byte green = (byte)(colourRgb.Green * 255 + 0.5);
         byte blue = (byte)(colourRgb.Blue * 255 + 0.5);
         return new Color(red, green, blue);
      }

      /// <summary>
      /// Get the RGB associated to the color, scaled by a normalised factor.
      /// </summary>
      /// <param name="factor">The normalised factor from 0 to 1.</param>
      /// <returns>The Color value.</returns>
      public static Color GetScaledColor(this IfcColourRgb colourRgb, double factor)
      {
         if (factor < MathUtil.Eps())
         {
            Importer.TheLog.LogWarning(colourRgb.StepId, "Invalid negative scaling factor of " + factor + ", defaulting to black.", true);
            return new Color(0, 0, 0);
         }

         Color origColor = colourRgb.CreateColor();
         if (origColor == null)
         {
            Importer.TheLog.LogError(colourRgb.StepId, "Couldn't create color, default to grey.", false);
            return DefaultColor();
         }

         if (factor > 1.0 + MathUtil.Eps())
         {
            Importer.TheLog.LogWarning(colourRgb.StepId, "Invalid normalised scaling factor of " + factor + ", defaulting to original color", true);
            return origColor;
         }

         byte red = (byte)(origColor.Red * 255 * factor + 0.5);
         byte green = (byte)(origColor.Green * 255 * factor + 0.5);
         byte blue = (byte)(origColor.Blue * 255 * factor + 0.5);
         return new Color(red, green, blue);
      }
   }
}