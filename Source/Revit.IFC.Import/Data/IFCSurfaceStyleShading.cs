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
   /// <summary>
   /// Class for IfcSurfaceStyleShading and subtype IfcSurfaceStyleRendering.
   /// </summary>
   public static class IFCSurfaceStyleShading
   {
      private static Color GetDefaultColor()
      {
         // Default to gray.
         return new Color(127, 127, 127);
      }

      private static Color processColor(IfcColourOrFactor colourOrFactor, IfcColourRgb surfaceColour)
      {
         IfcColourRgb colour = colourOrFactor as IfcColourRgb;
         if (colour != null)
            return colour.CreateColor();
         IfcNormalisedRatioMeasure normalisedRatioMeasure = colourOrFactor as IfcNormalisedRatioMeasure;
         if (normalisedRatioMeasure != null)
            return surfaceColour.GetScaledColor(normalisedRatioMeasure.Measure);
         return GetDefaultColor();
      }
      /// <summary>
      /// Return the diffuse color of the shading style.
      /// </summary>
      public static Color GetDiffuseColor(this IfcSurfaceStyleRendering rendering)
      {
         return processColor(rendering.DiffuseColour, rendering.SurfaceColour);
      }

      /// <summary>
      /// Return the transmission color of the shading style.
      /// </summary>
      public static Color GetTransmissionColor(this IfcSurfaceStyleRendering rendering)
      {
         return processColor(rendering.TransmissionColour, rendering.SurfaceColour);
      }

      /// <summary>
      /// Return the diffuse transmission color of the shading style.
      /// </summary>
      public static Color GetDiffuseTransmissionColor(this IfcSurfaceStyleRendering rendering)
      {
         return processColor(rendering.DiffuseTransmissionColour, rendering.SurfaceColour);
      }

      /// <summary>
      /// Return the reflection color of the shading style.
      /// </summary>
      public static Color GetReflectionColor(this IfcSurfaceStyleRendering rendering)
      {
         return processColor(rendering.ReflectionColour, rendering.SurfaceColour);
      }

      /// <summary>
      /// Return the specular color of the shading style.
      /// </summary>
      public static Color GetSpecularColor(this IfcSurfaceStyleRendering rendering)
      {
         return processColor(rendering.SpecularColour, rendering.SurfaceColour);
      }

      /// <summary>
      /// Calculates Revit shininess for a material based on the specular colour, if specified.
      /// </summary>
      public static int? GetSmoothness(this IfcSurfaceStyleRendering rendering)
      {
         IfcColourOrFactor colourOrFactor = rendering.SpecularColour;
         IfcNormalisedRatioMeasure normalisedRatioMeasure = colourOrFactor as IfcNormalisedRatioMeasure;
         if(normalisedRatioMeasure != null)
            return (int)(normalisedRatioMeasure.Measure * 100 + 0.5);
         IfcColourRgb specularColour = colourOrFactor as IfcColourRgb;
         if (specularColour == null)
            return null;

         // heuristic: get average of three components.
         double ave = (specularColour.Red + specularColour.Blue + specularColour.Green) / 3.0;
         return (int)(ave * 100 + 0.5);
      }

      
      /// <summary>
      /// Calculates Revit shininess for a material based on the specular highlight, if specified.
      /// </summary>
      public static int? GetShininess(this IfcSurfaceStyleRendering rendering)
      {
         int shininess = 0;
         string warning = null;

         // Assumes that m_SpecularExponent or m_SpecularShininess is set.
         // Validates that the value is in the range [0,128].
         IfcSpecularHighlightSelect highlight = rendering.SpecularHighlight;
         if (highlight == null)
            return null;
         IfcSpecularExponent specularExponent = highlight as IfcSpecularExponent;
         if (specularExponent != null)
         {
            shininess = (int)(specularExponent.SpecularExponent);
         }
         else
         {
            IfcSpecularRoughness specularRoughness = highlight as IfcSpecularRoughness;
            if (specularRoughness != null)
            {

               // m_SpecularRoughness is a real from [0,1] and is the reverse of our shininess.
               shininess = (int)((1.0 - specularRoughness.SpecularRoughness) * 128 + 0.5);

               if ((shininess < 0) || (shininess > 128))
                  warning = "Specular Roughness of " + specularRoughness.SpecularRoughness + " is of out range, should be between 0 and 1.";
            }
         }

         if (shininess < 0)
            shininess = 0;
         else if (shininess > 128)
            shininess = 128;

         if (shininess < 0)
            shininess = 0;
         else if (shininess > 128)
            shininess = 128;

         if (warning != null)
            Importer.TheLog.LogWarning(rendering.StepId, warning, true);

         return shininess;
      }

   }
}