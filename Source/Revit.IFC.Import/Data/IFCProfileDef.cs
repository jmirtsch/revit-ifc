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
   public class IFCProfileDef
   {
      private string m_ProfileName = null;

      private IfcProfileTypeEnum m_ProfileType;

      protected IFCProfileDef()
      {
      }
      protected IFCProfileDef(IfcProfileDef profileDef)
      {
         ProfileType = profileDef.ProfileType;
         ProfileName = profileDef.ProfileName;
      }

      /// <summary>
      /// Get the type of the profile.
      /// </summary>
      public IfcProfileTypeEnum ProfileType
      {
         get { return m_ProfileType; }
         protected set { m_ProfileType = value; }
      }

      /// <summary>
      /// Get the name of the profile.
      /// </summary>
      public string ProfileName
      {
         get { return m_ProfileName; }
         protected set { m_ProfileName = value; }
      }
   }

   // We may create more subclasses if we want to preserve the original parametric data.
   public class IFCParameterizedProfile : IFCSimpleProfile
   {
      private Arc CreateXYArc(XYZ center, double radius, double startAngle, double endAngle)
      {
         return Arc.Create(center, radius, startAngle, endAngle, XYZ.BasisX, XYZ.BasisY);
      }

      private Arc CreateReversedXYArc(XYZ center, double radius, double startAngle, double endAngle)
      {
         Arc arc = CreateXYArc(center, radius, startAngle, endAngle);
         return arc.CreateReversed() as Arc;
      }

      private Curve CreateXYEllipse(XYZ center, double radiusX, double radiusY, double startAngle, double endAngle)
      {
         return Ellipse.CreateCurve(center, radiusX, radiusY, XYZ.BasisX, XYZ.BasisY, startAngle, endAngle);
      }

      private CurveLoop CreatePolyCurveLoop(XYZ[] corners)
      {
         int sz = corners.Count();
         if (sz == 0)
            return null;

         CurveLoop curveLoop = new CurveLoop();
         for (int ii = 0; ii < sz; ii++)
            curveLoop.Append(Line.CreateBound(corners[ii], corners[(ii + 1) % sz]));
         return curveLoop;
      }

      private CurveLoop CreateFilletedRectangleCurveLoop(XYZ[] corners, double filletRadius)
      {
         int sz = corners.Count();
         if (sz != 4)
            return null;

         XYZ[] radii = new XYZ[4] {
                new XYZ( corners[0].X + filletRadius, corners[0].Y + filletRadius, 0.0 ),
                new XYZ( corners[1].X - filletRadius, corners[1].Y + filletRadius, 0.0 ),
                new XYZ( corners[2].X - filletRadius, corners[2].Y - filletRadius, 0.0 ),
                new XYZ( corners[3].X + filletRadius, corners[3].Y - filletRadius, 0.0 ),
            };

         XYZ[] fillets = new XYZ[8] {
                new XYZ( corners[0].X, corners[0].Y + filletRadius, 0.0 ),
                new XYZ( corners[0].X + filletRadius, corners[0].Y, 0.0 ),
                new XYZ( corners[1].X - filletRadius, corners[1].Y, 0.0 ),
                new XYZ( corners[1].X, corners[1].Y + filletRadius, 0.0 ),
                new XYZ( corners[2].X, corners[2].Y - filletRadius, 0.0 ),
                new XYZ( corners[2].X - filletRadius, corners[2].Y, 0.0 ),
                new XYZ( corners[3].X + filletRadius, corners[3].Y, 0.0 ),
                new XYZ( corners[3].X, corners[3].Y - filletRadius, 0.0 )
            };

         CurveLoop curveLoop = new CurveLoop();
         for (int ii = 0; ii < 4; ii++)
         {
            curveLoop.Append(Line.CreateBound(fillets[ii * 2 + 1], fillets[(ii * 2 + 2) % 8]));
            double startAngle = Math.PI * ((ii + 3) % 4) / 2;
            curveLoop.Append(CreateXYArc(radii[(ii + 1) % 4], filletRadius, startAngle, startAngle + Math.PI / 2));
         }

         return curveLoop;
      }

      internal IFCParameterizedProfile(IfcRectangleProfileDef profileDef)
         : base(profileDef)
      {
         Position = profileDef.Position.GetAxis2Placement2DTransform();
         double xDimVal = IFCUnitUtil.ScaleLength(profileDef.XDim), yDimVal = IFCUnitUtil.ScaleLength(profileDef.YDim);
         XYZ[] corners = new XYZ[4] {
                new XYZ( -xDimVal/2.0, -yDimVal/2.0, 0.0 ),
                new XYZ( xDimVal/2.0, -yDimVal/2.0, 0.0 ),
                new XYZ( xDimVal/2.0, yDimVal/2.0, 0.0 ),
                new XYZ( -xDimVal/2.0, yDimVal/2.0, 0.0 )
            };

         double outerFilletRadius = 0;
         IfcRectangleHollowProfileDef rectangleHollowProfileDef = profileDef as IfcRectangleHollowProfileDef;
         if (rectangleHollowProfileDef != null)
            outerFilletRadius = IFCUnitUtil.ScaleLength(rectangleHollowProfileDef.OuterFilletRadius);
         else
         { 
            IfcRoundedRectangleProfileDef roundedRectangleProfileDef = profileDef as IfcRoundedRectangleProfileDef;
            if (roundedRectangleProfileDef != null)
               outerFilletRadius = IFCUnitUtil.ScaleLength(roundedRectangleProfileDef.RoundingRadius);
         }
         if (outerFilletRadius > MathUtil.Eps() && !double.IsNaN(outerFilletRadius) && (outerFilletRadius < ((Math.Min(xDimVal, yDimVal) / 2.0) - MathUtil.Eps())))
            OuterCurve = CreateFilletedRectangleCurveLoop(corners, outerFilletRadius);

         if (OuterCurve == null)
            OuterCurve = CreatePolyCurveLoop(corners);

         if (rectangleHollowProfileDef != null)
         {
            double wallThickness = rectangleHollowProfileDef.WallThickness;
            if (wallThickness > MathUtil.Eps() && !double.IsNaN(wallThickness) && (wallThickness < ((Math.Min(xDimVal, yDimVal) / 2.0) - MathUtil.Eps())))
            {
               double innerXDimVal = xDimVal - wallThickness * 2.0;
               double innerYDimVal = yDimVal - wallThickness * 2.0;
               XYZ[] innerCorners = new XYZ[4] {
                    new XYZ( -innerXDimVal/2.0, -innerYDimVal/2.0, 0.0 ),
                    new XYZ( innerXDimVal/2.0, -innerYDimVal/2.0, 0.0 ),
                    new XYZ( innerXDimVal/2.0, innerYDimVal/2.0, 0.0 ),
                    new XYZ( -innerXDimVal/2.0, innerYDimVal/2.0, 0.0 )
                };

               double innerFilletRadius = rectangleHollowProfileDef.InnerFilletRadius;
               if (!double.IsNaN(innerFilletRadius) && innerFilletRadius > MathUtil.Eps() && (innerFilletRadius < ((Math.Min(innerXDimVal, innerYDimVal) / 2.0) - MathUtil.Eps())))
                  InnerCurves.Add(CreateFilletedRectangleCurveLoop(innerCorners, innerFilletRadius));

               if (InnerCurves.Count == 0)
                  InnerCurves.Add(CreatePolyCurveLoop(innerCorners));
            }
         }
      }

      internal IFCParameterizedProfile(IfcCircleProfileDef profileDef)
         : base(profileDef)
      {
         Position = profileDef.Position.GetAxis2Placement2DTransform();
         double radius = IFCUnitUtil.ScaleLength(profileDef.Radius);

         if (radius < MathUtil.Eps())
            Importer.TheLog.LogError(profileDef.StepId, "IfcCircleProfileDef has invalid radius: " + radius + ", ignoring.", true);

         // Some internal routines want CurveLoops with bounded components.  Split to avoid problems.
         OuterCurve = new CurveLoop();
         OuterCurve.Append(CreateXYArc(XYZ.Zero, radius, 0, Math.PI));
         OuterCurve.Append(CreateXYArc(XYZ.Zero, radius, Math.PI, 2 * Math.PI));

         IfcCircleHollowProfileDef circleHollowProfileDef = profileDef as IfcCircleHollowProfileDef;
         if (circleHollowProfileDef != null)
         {
            double wallThickness = IFCUnitUtil.ScaleLength(circleHollowProfileDef.WallThickness);
            if (wallThickness > MathUtil.Eps() && wallThickness < radius)
            {
               double innerRadius = radius - wallThickness;

               CurveLoop innerCurve = new CurveLoop();
               innerCurve.Append(CreateXYArc(XYZ.Zero, innerRadius, 0, Math.PI));
               innerCurve.Append(CreateXYArc(XYZ.Zero, innerRadius, Math.PI, 2 * Math.PI));

               InnerCurves.Add(innerCurve);
            }
         }
      }

      internal IFCParameterizedProfile(IfcEllipseProfileDef profileDef)
         : base(profileDef)
      {
         Position = profileDef.Position.GetAxis2Placement2DTransform();
         double radiusX = IFCUnitUtil.ScaleLength(profileDef.SemiAxis1);
         double radiusY = IFCUnitUtil.ScaleLength(profileDef.SemiAxis2);

         // Some internal routines want CurveLoops with bounded components.  Split to avoid problems.
         OuterCurve = new CurveLoop();
         OuterCurve.Append(CreateXYEllipse(XYZ.Zero, radiusX, radiusY, 0, Math.PI));
         OuterCurve.Append(CreateXYEllipse(XYZ.Zero, radiusX, radiusY, Math.PI, 2 * Math.PI));
      }

      internal IFCParameterizedProfile(IfcCShapeProfileDef profileDef)
         : base(profileDef)
      {
         Position = profileDef.Position.GetAxis2Placement2DTransform();
         double depth = IFCUnitUtil.ScaleLength(profileDef.Depth);
         double width = IFCUnitUtil.ScaleLength(profileDef.Width);
         double wallThickness = IFCUnitUtil.ScaleLength(profileDef.WallThickness);

         double girth = IFCUnitUtil.ScaleLength(profileDef.Girth);

         double centerOptX = 0;// Centre of Gravity shouldn't transform physical profile  IFCImportHandleUtil.GetOptionalScaledLengthAttribute(profileDef, "CentreOfGravityInX", 0.0);
         double innerRadius = IFCUnitUtil.ScaleLength(profileDef.InternalFilletRadius);

         bool hasFillet = !MathUtil.IsAlmostZero(innerRadius) && !double.IsNaN(innerRadius);
         double outerRadius = hasFillet ? innerRadius + wallThickness : 0.0;

         XYZ[] cShapePoints = new XYZ[12] {
                new XYZ(width/2.0 + centerOptX, -depth/2.0+girth, 0.0),
                new XYZ(width/2.0 + centerOptX, -depth/2.0, 0.0),
                new XYZ(-width/2.0 + centerOptX, -depth/2.0, 0.0),
                new XYZ(-width/2.0 + centerOptX, depth/2.0, 0.0),
                new XYZ(width/2.0 + centerOptX, depth/2.0, 0.0),
                new XYZ(width/2.0 + centerOptX, -(-depth/2.0+girth), 0.0),
                new XYZ(width/2.0 - wallThickness, -(-depth/2.0+girth), 0.0),
                new XYZ(width/2.0 - wallThickness, depth/2.0 - wallThickness, 0.0),
                new XYZ(-width/2.0 + wallThickness, depth/2.0 - wallThickness, 0.0),
                new XYZ(-width/2.0 + wallThickness, -depth/2.0 + wallThickness, 0.0),
                new XYZ(width/2.0 - wallThickness, -depth/2.0 + wallThickness, 0.0),
                new XYZ(width/2.0 + centerOptX - wallThickness, -depth/2.0+girth, 0.0)
            };

         OuterCurve = new CurveLoop();
         if (hasFillet)
         {
            XYZ[] cFilletPoints = new XYZ[16] {
                    new XYZ(cShapePoints[1][0], cShapePoints[1][1] + outerRadius, 0.0),
                    new XYZ(cShapePoints[1][0] - outerRadius, cShapePoints[1][1], 0.0),
                    new XYZ(cShapePoints[2][0] + outerRadius, cShapePoints[2][1], 0.0),
                    new XYZ(cShapePoints[2][0], cShapePoints[2][1] + outerRadius, 0.0),
                    new XYZ(cShapePoints[3][0], cShapePoints[3][1] - outerRadius, 0.0),
                    new XYZ(cShapePoints[3][0] + outerRadius, cShapePoints[3][1], 0.0),
                    new XYZ(cShapePoints[4][0] - outerRadius, cShapePoints[4][1], 0.0),
                    new XYZ(cShapePoints[4][0], cShapePoints[4][1] - outerRadius, 0.0),
                    new XYZ(cShapePoints[7][0], cShapePoints[7][1] - innerRadius, 0.0),
                    new XYZ(cShapePoints[7][0] - innerRadius, cShapePoints[7][1], 0.0),
                    new XYZ(cShapePoints[8][0] + innerRadius, cShapePoints[8][1], 0.0),
                    new XYZ(cShapePoints[8][0], cShapePoints[8][1] - innerRadius, 0.0),
                    new XYZ(cShapePoints[9][0], cShapePoints[9][1] + innerRadius, 0.0),
                    new XYZ(cShapePoints[9][0] + innerRadius, cShapePoints[9][1], 0.0),
                    new XYZ(cShapePoints[10][0] - innerRadius, cShapePoints[10][1], 0.0),
                    new XYZ(cShapePoints[10][0], cShapePoints[10][1] + innerRadius, 0.0)
                };

            // shared for inner and outer.
            XYZ[] cFilletCenters = new XYZ[4] {
                    new XYZ(cShapePoints[1][0] - outerRadius, cShapePoints[1][1] + outerRadius, 0.0),
                    new XYZ(cShapePoints[2][0] + outerRadius, cShapePoints[2][1] + outerRadius, 0.0),
                    new XYZ(cShapePoints[3][0] + outerRadius, cShapePoints[3][1] - outerRadius, 0.0),
                    new XYZ(cShapePoints[4][0] - outerRadius, cShapePoints[4][1] - outerRadius, 0.0)
                };

            // flip outers not inners.
            double[][] cRange = new double[4][] {
                    new double[2] { 3*Math.PI/2.0, 2.0*Math.PI },
                    new double[2] { Math.PI, 3*Math.PI/2.0 },
                    new double[2] { Math.PI/2.0, Math.PI },
                    new double[2] { 0.0, Math.PI/2.0 }
                };

            OuterCurve.Append(Line.CreateBound(cShapePoints[0], cFilletPoints[0]));
            for (int ii = 0; ii < 3; ii++)
            {
               OuterCurve.Append(CreateReversedXYArc(cFilletCenters[ii], outerRadius, cRange[ii][0], cRange[ii][1]));

               OuterCurve.Append(Line.CreateBound(cFilletPoints[2 * ii + 1], cFilletPoints[2 * ii + 2]));

               OuterCurve.Append(CreateReversedXYArc(cFilletCenters[3], outerRadius, cRange[3][0], cRange[3][1]));

               OuterCurve.Append(Line.CreateBound(cFilletPoints[7], cShapePoints[5]));
               OuterCurve.Append(Line.CreateBound(cShapePoints[5], cShapePoints[6]));
               OuterCurve.Append(Line.CreateBound(cShapePoints[6], cFilletPoints[8]));

               for (int jj = 0; jj < 3; ii++)
               {
                  OuterCurve.Append(CreateXYArc(cFilletCenters[3 - jj], innerRadius, cRange[3 - jj][0], cRange[3 - jj][1]));
                  OuterCurve.Append(Line.CreateBound(cFilletPoints[2 * jj + 9], cFilletPoints[2 * jj + 10]));
               }

               OuterCurve.Append(CreateXYArc(cFilletCenters[0], innerRadius, cRange[0][0], cRange[0][1]));

               OuterCurve.Append(Line.CreateBound(cFilletPoints[15], cShapePoints[11]));
               OuterCurve.Append(Line.CreateBound(cShapePoints[11], cShapePoints[0]));
            }
         }
         else
         {
            OuterCurve = CreatePolyCurveLoop(cShapePoints);
         }
      }

      internal IFCParameterizedProfile(IfcLShapeProfileDef profileDef)
         : base(profileDef)
      {
         Position = profileDef.Position.GetAxis2Placement2DTransform();
         double depth = IFCUnitUtil.ScaleLength(profileDef.Depth);
         double thickness = IFCUnitUtil.ScaleLength(profileDef.Thickness);
         double width = IFCUnitUtil.ScaleLength(profileDef.Width);

         double filletRadius = IFCUnitUtil.ScaleLength(profileDef.FilletRadius);
         bool filletedCorner = !MathUtil.IsAlmostZero(filletRadius) && !double.IsNaN(filletRadius);

         double edgeRadius = IFCUnitUtil.ScaleLength(profileDef.EdgeRadius);
         bool filletedEdge = !MathUtil.IsAlmostZero(edgeRadius) && !double.IsNaN(edgeRadius);
         if (filletedEdge && (thickness < edgeRadius - MathUtil.Eps()))
         {
            // LOG: WARN: IFC: In IfcLShapeProfileDef (#id), edgeRadius (value) >= thickness (value), ignoring."
            filletedEdge = false;
         }
         bool fullFilletedEdge = (filletedEdge && MathUtil.IsAlmostEqual(thickness, edgeRadius));

         double centerOptX = 0;// IFCImportHandleUtil.GetOptionalScaledLengthAttribute(profileDef, "CentreOfGravityInX", 0.0);

         double centerOptY = 0;// IFCImportHandleUtil.GetOptionalScaledLengthAttribute(profileDef, "CentreOfGravityInY", centerOptX);

         // TODO: use leg slope
         double legSlope = IFCUnitUtil.ScaleAngle(profileDef.LegSlope);// IFCImportHandleUtil.GetOptionalScaledAngleAttribute(profileDef, "LegSlope", 0.0);
         if (double.IsNaN(legSlope))
            legSlope = 0;

         XYZ lOrig = new XYZ(-width / 2.0 + centerOptX, -depth / 2.0 + centerOptY, 0.0);
         XYZ lLR = new XYZ(lOrig[0] + width, lOrig[1], 0.0);
         XYZ lLRPlusThickness = new XYZ(lLR[0], lLR[1] + thickness, 0.0);
         XYZ lCorner = new XYZ(lOrig[0] + thickness, lOrig[1] + thickness, 0.0);
         XYZ lULPlusThickness = new XYZ(lOrig[0] + thickness, lOrig[1] + depth, 0.0);
         XYZ lUL = new XYZ(lULPlusThickness[0] - thickness, lULPlusThickness[1], 0.0);

         // fillet modifications.
         double[] edgeRanges = new double[2];
         XYZ lLREdgeCtr = null, lULEdgeCtr = null;
         XYZ lLRStartFillet = null, lLREndFillet = null;
         XYZ lULStartFillet = null, lULEndFillet = null;

         if (filletedEdge)
         {
            lLREdgeCtr = new XYZ(lLRPlusThickness[0] - edgeRadius, lLRPlusThickness[1] - edgeRadius, 0.0);
            lULEdgeCtr = new XYZ(lULPlusThickness[0] - edgeRadius, lULPlusThickness[1] - edgeRadius, 0.0);

            lLRStartFillet = new XYZ(lLRPlusThickness[0], lLRPlusThickness[1] - edgeRadius, 0.0);
            lLREndFillet = new XYZ(lLRPlusThickness[0] - edgeRadius, lLRPlusThickness[1], 0.0);

            lULStartFillet = new XYZ(lULPlusThickness[0], lULPlusThickness[1] - edgeRadius, 0.0);
            lULEndFillet = new XYZ(lULPlusThickness[0] - edgeRadius, lULPlusThickness[1], 0.0);

            edgeRanges[0] = 0.0; edgeRanges[1] = Math.PI / 2.0;
         }

         XYZ lLRCorner = null, lULCorner = null, lFilletCtr = null;
         double[] filletRange = new double[2];
         if (filletedCorner)
         {
            lLRCorner = new XYZ(lCorner[0] + filletRadius, lCorner[1], lCorner[2]);
            lULCorner = new XYZ(lCorner[0], lCorner[1] + filletRadius, lCorner[2]);
            lFilletCtr = new XYZ(lCorner[0] + filletRadius, lCorner[1] + filletRadius, lCorner[2]);

            filletRange[0] = Math.PI; filletRange[1] = 3.0 * Math.PI / 2;
         }

         OuterCurve = new CurveLoop();

         OuterCurve.Append(Line.CreateBound(lOrig, lLR));

         XYZ startCornerPoint = null, endCornerPoint = null;
         if (filletedEdge)
         {
            startCornerPoint = lLREndFillet;
            endCornerPoint = lULStartFillet;
         }
         else
         {
            startCornerPoint = lLRPlusThickness;
            endCornerPoint = lULPlusThickness;
         }

         if (filletedEdge)
         {
            if (!fullFilletedEdge)
            {
               OuterCurve.Append(Line.CreateBound(lLR, lLRStartFillet));
            }

            OuterCurve.Append(CreateXYArc(lLREdgeCtr, edgeRadius, edgeRanges[0], edgeRanges[1]));
         }
         else
         {
            OuterCurve.Append(Line.CreateBound(lLR, startCornerPoint));
         }

         if (filletedCorner)
         {
            OuterCurve.Append(Line.CreateBound(startCornerPoint, lLRCorner));
            OuterCurve.Append(CreateReversedXYArc(lFilletCtr, filletRadius, filletRange[0], filletRange[1]));
            OuterCurve.Append(Line.CreateBound(lULCorner, endCornerPoint));
         }
         else
         {
            OuterCurve.Append(Line.CreateBound(startCornerPoint, lCorner));
            OuterCurve.Append(Line.CreateBound(lCorner, endCornerPoint));
         }

         if (filletedEdge)
         {
            OuterCurve.Append(CreateXYArc(lULEdgeCtr, edgeRadius, edgeRanges[0], edgeRanges[1]));
            if (!fullFilletedEdge)
            {
               OuterCurve.Append(Line.CreateBound(lULEndFillet, lUL));
            }
         }
         else
         {
            OuterCurve.Append(Line.CreateBound(endCornerPoint, lUL));
         }

         OuterCurve.Append(Line.CreateBound(lUL, lOrig));
      }

      internal IFCParameterizedProfile(IfcIShapeProfileDef profileDef)
         : base(profileDef)
      {
         Position = profileDef.Position.GetAxis2Placement2DTransform();
         double width = IFCUnitUtil.ScaleLength(profileDef.OverallWidth);
         double depth = IFCUnitUtil.ScaleLength(profileDef.OverallDepth);
         double webThickness = IFCUnitUtil.ScaleLength(profileDef.WebThickness);
         double flangeThickness = IFCUnitUtil.ScaleLength(profileDef.FlangeThickness);
         double filletRadius = IFCUnitUtil.ScaleLength(profileDef.FilletRadius);
         bool hasFillet = !MathUtil.IsAlmostZero(filletRadius) && !double.IsNaN(filletRadius);

         // take advantage of X/Y symmetries below.
         XYZ[] iShapePoints = new XYZ[12] {
                new XYZ(-width/2.0, -depth/2.0, 0.0),
                new XYZ(width/2.0, -depth/2.0, 0.0),
                new XYZ(width/2.0, -depth/2.0 + flangeThickness, 0.0),
                new XYZ(webThickness/2.0, -depth/2.0 + flangeThickness, 0.0),

                new XYZ(webThickness/2.0, -(-depth/2.0 + flangeThickness), 0.0),
                new XYZ(width/2.0, -(-depth/2.0 + flangeThickness), 0.0),
                new XYZ(width/2.0, depth/2.0, 0.0),
                new XYZ(-width/2.0, depth/2.0, 0.0),

                new XYZ(-width/2.0,  -(-depth/2.0 + flangeThickness), 0.0),
                new XYZ(-webThickness/2.0,  -(-depth/2.0 + flangeThickness), 0.0),
                new XYZ(-webThickness/2.0,  -depth/2.0 + flangeThickness, 0.0),
                new XYZ(-width/2.0,  -depth/2.0 + flangeThickness, 0.0)
            };

         if (hasFillet)
         {
            OuterCurve = new CurveLoop();
            XYZ[] iFilletPoints = new XYZ[8] {
                    new XYZ(iShapePoints[3][0] + filletRadius, iShapePoints[3][1], 0.0),
                    new XYZ(iShapePoints[3][0], iShapePoints[3][1] + filletRadius, 0.0),
                    new XYZ(iShapePoints[4][0], iShapePoints[4][1] - filletRadius, 0.0),
                    new XYZ(iShapePoints[4][0] + filletRadius, iShapePoints[4][1], 0.0),
                    new XYZ(iShapePoints[9][0] - filletRadius, iShapePoints[9][1], 0.0),
                    new XYZ(iShapePoints[9][0], iShapePoints[9][1] - filletRadius, 0.0),
                    new XYZ(iShapePoints[10][0], iShapePoints[10][1] + filletRadius, 0.0),
                    new XYZ(iShapePoints[10][0] - filletRadius, iShapePoints[10][1], 0.0)
                };

            XYZ[] iFilletCtr = new XYZ[4] {
                    new XYZ(iShapePoints[3][0] + filletRadius, iShapePoints[3][1] + filletRadius, 0.0),
                    new XYZ(iShapePoints[4][0] + filletRadius, iShapePoints[4][1] - filletRadius, 0.0),
                    new XYZ(iShapePoints[9][0] - filletRadius, iShapePoints[9][1] - filletRadius, 0.0),
                    new XYZ(iShapePoints[10][0] - filletRadius, iShapePoints[10][1] + filletRadius, 0.0)
                };

            // need to flip all fillets.
            double[][] filletRanges = new double[4][] {
                    new double[2] { Math.PI, 3.0*Math.PI/2 },
                    new double[2] { Math.PI/2.0, Math.PI },
                    new double[2] { 0, Math.PI/2.0 },
                    new double[2] { 3.0*Math.PI/2, 2.0*Math.PI }
                };

            OuterCurve.Append(Line.CreateBound(iShapePoints[0], iShapePoints[1]));
            OuterCurve.Append(Line.CreateBound(iShapePoints[1], iShapePoints[2]));
            OuterCurve.Append(Line.CreateBound(iShapePoints[2], iFilletPoints[0]));

            OuterCurve.Append(CreateReversedXYArc(iFilletCtr[0], filletRadius, filletRanges[0][0], filletRanges[0][1]));

            OuterCurve.Append(Line.CreateBound(iFilletPoints[1], iFilletPoints[2]));

            OuterCurve.Append(CreateReversedXYArc(iFilletCtr[1], filletRadius, filletRanges[1][0], filletRanges[1][1]));

            OuterCurve.Append(Line.CreateBound(iFilletPoints[3], iShapePoints[5]));
            OuterCurve.Append(Line.CreateBound(iShapePoints[5], iShapePoints[6]));
            OuterCurve.Append(Line.CreateBound(iShapePoints[6], iShapePoints[7]));
            OuterCurve.Append(Line.CreateBound(iShapePoints[7], iShapePoints[8]));
            OuterCurve.Append(Line.CreateBound(iShapePoints[8], iFilletPoints[4]));

            OuterCurve.Append(CreateReversedXYArc(iFilletCtr[2], filletRadius, filletRanges[2][0], filletRanges[2][1]));

            OuterCurve.Append(Line.CreateBound(iFilletPoints[5], iFilletPoints[6]));

            OuterCurve.Append(CreateReversedXYArc(iFilletCtr[3], filletRadius, filletRanges[3][0], filletRanges[3][1]));

            OuterCurve.Append(Line.CreateBound(iFilletPoints[7], iShapePoints[11]));
            OuterCurve.Append(Line.CreateBound(iShapePoints[11], iShapePoints[0]));
         }
         else
         {
            OuterCurve = CreatePolyCurveLoop(iShapePoints);
         }
      }

      internal IFCParameterizedProfile(IfcTShapeProfileDef profileDef)
         : base(profileDef)
      {
         Position = profileDef.Position.GetAxis2Placement2DTransform();
         double flangeWidth = IFCUnitUtil.ScaleLength(profileDef.FlangeWidth);
         double depth = IFCUnitUtil.ScaleLength(profileDef.Depth);
         double webThickness = IFCUnitUtil.ScaleLength(profileDef.WebThickness);
         double flangeThickness = IFCUnitUtil.ScaleLength(profileDef.FlangeThickness);

         double centerOptY = 0;// IFCImportHandleUtil.GetOptionalScaledLengthAttribute(profileDef, "CentreOfGravityInY", 0.0);

         double filletRadius = IFCUnitUtil.ScaleLength(profileDef.FilletRadius);
         bool hasFillet = !MathUtil.IsAlmostZero(filletRadius) && !double.IsNaN(filletRadius);

         double flangeEdgeRadius = IFCUnitUtil.ScaleLength(profileDef.FlangeEdgeRadius);
         bool hasFlangeEdge = !MathUtil.IsAlmostZero(flangeEdgeRadius) && !double.IsNaN(flangeEdgeRadius);

         double webEdgeRadius = IFCUnitUtil.ScaleLength(profileDef.WebEdgeRadius);
         bool hasWebEdge = !MathUtil.IsAlmostZero(webEdgeRadius) && !double.IsNaN(webEdgeRadius);

         double webSlope = IFCUnitUtil.ScaleAngle(profileDef.WebSlope);
         if (double.IsNaN(webSlope))
            webSlope = 0;
         double webDeltaX = (depth / 2.0) * Math.Sin(webSlope);
         XYZ webDir = new XYZ(-Math.Sin(webSlope), Math.Cos(webSlope), 0.0);

         double flangeSlope = IFCUnitUtil.ScaleAngle(profileDef.FlangeSlope);
         if (double.IsNaN(flangeSlope))
            flangeSlope = 0;
         double flangeDeltaY = (flangeWidth / 4.0) * Math.Sin(flangeSlope);
         XYZ flangeDir = new XYZ(Math.Cos(flangeSlope), -Math.Sin(flangeSlope), 0.0);

         XYZ[] tShapePoints = new XYZ[8] {
                new XYZ(-flangeWidth/2.0, depth /2.0 + centerOptY, 0.0),
                new XYZ(-flangeWidth/2.0, depth/2.0 + centerOptY - (flangeThickness-flangeDeltaY), 0.0),
                new XYZ(0.0, 0.0, 0.0),   // calc below
                new XYZ(-webThickness/2.0 + webDeltaX, -depth/2.0 + centerOptY, 0.0),
                new XYZ(-(-webThickness/2.0 + webDeltaX), -depth/2.0 + centerOptY, 0.0),
                new XYZ(0.0, 0.0, 0.0),   // calc below
                new XYZ(flangeWidth/2.0, depth/2.0 + centerOptY - (flangeThickness-flangeDeltaY), 0.0),
                new XYZ(flangeWidth/2.0, depth/2.0 + centerOptY, 0.0)
            };

         Line line1 = Line.CreateUnbound(tShapePoints[1], flangeDir);
         Line line2 = Line.CreateUnbound(tShapePoints[3], webDir);

         IntersectionResultArray intersectResultArray;
         SetComparisonResult intersectResultComp = line1.Intersect(line2, out intersectResultArray);
         if ((intersectResultComp == SetComparisonResult.Overlap) && (intersectResultArray.Size == 1))
            tShapePoints[2] = intersectResultArray.get_Item(0).XYZPoint;
         else
         {
            // LOG: ERROR: Couldn't calculate point in IfcTShapeProfileDef (#%d).
            return;
         }
         tShapePoints[5] = new XYZ(-tShapePoints[2][0], tShapePoints[2][1], tShapePoints[2][2]);

         // TODO: support fillets!
         OuterCurve = CreatePolyCurveLoop(tShapePoints);
      }

      internal IFCParameterizedProfile(IfcUShapeProfileDef profileDef)
         : base(profileDef)
      {
         Position = profileDef.Position.GetAxis2Placement2DTransform();
         double flangeWidth = IFCUnitUtil.ScaleLength(profileDef.FlangeWidth);
         double depth = IFCUnitUtil.ScaleLength(profileDef.Depth);
         double webThickness = IFCUnitUtil.ScaleLength(profileDef.WebThickness);
         double flangeThickness = IFCUnitUtil.ScaleLength(profileDef.FlangeThickness);
         double centerOptX = 0;// IFCImportHandleUtil.GetOptionalScaledLengthAttribute(profileDef, "CentreOfGravityInX", 0.0);

         double filletRadius = IFCUnitUtil.ScaleLength(profileDef.FilletRadius);
         bool hasFillet = !MathUtil.IsAlmostZero(filletRadius) && !double.IsNaN(filletRadius);

         double edgeRadius = IFCUnitUtil.ScaleLength(profileDef.EdgeRadius);
         bool hasEdgeRadius = !MathUtil.IsAlmostZero(edgeRadius) && !double.IsNaN(edgeRadius);

         double flangeSlope = IFCUnitUtil.ScaleAngle(profileDef.FlangeSlope);
         if (double.IsNaN(flangeSlope))
            flangeSlope = 0;
         double flangeDirY = Math.Sin(flangeSlope);

         // start lower left, CCW.
         XYZ[] uShapePoints = new XYZ[8] {
                new XYZ(-flangeWidth/2.0+centerOptX, -depth/2.0, 0.0),
                new XYZ(flangeWidth/2.0+centerOptX, -depth/2.0, 0.0),
                new XYZ(flangeWidth/2.0+centerOptX, -depth/2.0 + (flangeThickness-flangeDirY*(flangeWidth/2.0)), 0.0),
                new XYZ(-flangeWidth/2.0+centerOptX+webThickness, -depth/2.0 + (flangeThickness+flangeDirY*(flangeWidth/2.0-webThickness)), 0.0),
                new XYZ(-flangeWidth/2.0+centerOptX+webThickness, -(-depth/2.0 + (flangeThickness+flangeDirY*(flangeWidth/2.0-webThickness))), 0.0),
                new XYZ(flangeWidth/2.0+centerOptX, -(-depth/2.0 + (flangeThickness-flangeDirY*(flangeWidth/2.0))), 0.0),
                new XYZ(flangeWidth/2.0+centerOptX, depth/2.0, 0.0),
                new XYZ(-flangeWidth/2.0+centerOptX, depth/2.0, 0.0),
            };

         // TODO: support fillets!
         OuterCurve = CreatePolyCurveLoop(uShapePoints);
      }

      internal IFCParameterizedProfile(IfcZShapeProfileDef profileDef)
         : base(profileDef)
      {
         Position = profileDef.Position.GetAxis2Placement2DTransform();
         double flangeWidth = IFCUnitUtil.ScaleLength(profileDef.FlangeWidth);
         double depth = IFCUnitUtil.ScaleLength(profileDef.Depth);
         double webThickness = IFCUnitUtil.ScaleLength(profileDef.WebThickness);
         double flangeThickness = IFCUnitUtil.ScaleLength(profileDef.FlangeThickness);

         double filletRadius = IFCUnitUtil.ScaleLength(profileDef.FilletRadius);
         bool hasFillet = !MathUtil.IsAlmostZero(filletRadius) && !double.IsNaN(filletRadius);

         double edgeRadius = IFCUnitUtil.ScaleLength(profileDef.EdgeRadius);
         bool hasEdgeRadius = !MathUtil.IsAlmostZero(edgeRadius) && !double.IsNaN(edgeRadius);

         XYZ[] zShapePoints = new XYZ[8] {
                new XYZ(-webThickness/2.0, -depth/2.0, 0.0),
                new XYZ(flangeWidth - webThickness/2.0, -depth/2.0, 0.0),
                new XYZ(flangeWidth - webThickness/2.0, flangeThickness - depth/2.0, 0.0),
                new XYZ(webThickness/2.0, flangeThickness - depth/2.0, 0.0),
                new XYZ(webThickness/2.0, depth/2.0, 0.0),
                new XYZ(webThickness/2.0 - flangeWidth, depth/2.0, 0.0),
                new XYZ(webThickness/2.0 - flangeWidth, depth/2.0 - flangeThickness, 0.0),
                new XYZ(-webThickness/2.0, depth/2.0 - flangeThickness, 0.0)
            };

         // need to flip fillet arcs.
         XYZ[] zFilletPoints = new XYZ[4] {
                new XYZ(zShapePoints[3][0] + filletRadius, zShapePoints[3][1], 0.0),
                new XYZ(zShapePoints[3][0], zShapePoints[3][1] + filletRadius, 0.0),
                new XYZ(zShapePoints[7][0] - filletRadius, zShapePoints[7][1], 0.0),
                new XYZ(zShapePoints[7][0], zShapePoints[7][1] - filletRadius, 0.0)
            };

         XYZ[] zFilletCenters = new XYZ[2] {
                new XYZ(zShapePoints[3][0] + filletRadius, zShapePoints[3][1] + filletRadius, 0.0),
                new XYZ(zShapePoints[7][0] - filletRadius, zShapePoints[7][1] - filletRadius, 0.0),
            };

         double[][] filletRange = new double[2][] {
                new double[2] { Math.PI, 3*Math.PI/2.0 },
                new double[2] { 0.0, Math.PI/2.0 }
            };

         // do not flip edge arcs.
         XYZ[] zEdgePoints = new XYZ[4] {
                new XYZ(zShapePoints[2][0], zShapePoints[2][1] - edgeRadius, 0.0),
                new XYZ(zShapePoints[2][0] - edgeRadius, zShapePoints[2][1], 0.0),
                new XYZ(zShapePoints[6][0], zShapePoints[6][1] + edgeRadius, 0.0),
                new XYZ(zShapePoints[6][0] + edgeRadius, zShapePoints[6][1], 0.0)
            };

         XYZ[] zEdgeCenters = new XYZ[2] {
                new XYZ(zShapePoints[2][0] - edgeRadius, zShapePoints[2][1] - edgeRadius, 0.0),
                new XYZ(zShapePoints[6][0] + edgeRadius, zShapePoints[6][1] + edgeRadius, 0.0)
            };

         double[][] edgeRange = new double[2][] {
                new double[2] { 0.0, Math.PI/2.0 },
                new double[2] { Math.PI, 3*Math.PI/2.0 }
            };

         OuterCurve = new CurveLoop();

         OuterCurve.Append(Line.CreateBound(zShapePoints[0], zShapePoints[1]));

         XYZ zNextStart = null;
         if (hasEdgeRadius)
         {
            OuterCurve.Append(Line.CreateBound(zShapePoints[1], zEdgePoints[0]));
            OuterCurve.Append(CreateXYArc(zEdgeCenters[0], edgeRadius, edgeRange[0][0], edgeRange[0][1]));
            zNextStart = zEdgePoints[1];
         }
         else
         {
            OuterCurve.Append(Line.CreateBound(zShapePoints[1], zShapePoints[2]));
            zNextStart = zShapePoints[2];
         }

         if (hasFillet)
         {
            OuterCurve.Append(Line.CreateBound(zNextStart, zFilletPoints[0]));

            OuterCurve.Append(CreateReversedXYArc(zFilletCenters[0], filletRadius, filletRange[0][0], filletRange[0][1]));
            zNextStart = zFilletPoints[1];
         }
         else
         {
            OuterCurve.Append(Line.CreateBound(zNextStart, zShapePoints[3]));
            zNextStart = zShapePoints[3];
         }

         OuterCurve.Append(Line.CreateBound(zNextStart, zShapePoints[4]));
         OuterCurve.Append(Line.CreateBound(zShapePoints[4], zShapePoints[5]));

         if (hasEdgeRadius)
         {
            OuterCurve.Append(Line.CreateBound(zShapePoints[5], zEdgePoints[2]));
            OuterCurve.Append(CreateXYArc(zEdgeCenters[1], edgeRadius, edgeRange[1][0], edgeRange[1][1]));
            zNextStart = zEdgePoints[3];
         }
         else
         {
            OuterCurve.Append(Line.CreateBound(zShapePoints[5], zShapePoints[6]));
            zNextStart = zShapePoints[6];
         }

         if (hasFillet)
         {
            OuterCurve.Append(Line.CreateBound(zNextStart, zFilletPoints[2]));

            OuterCurve.Append(CreateReversedXYArc(zFilletCenters[1], filletRadius, filletRange[1][0], filletRange[1][1]));
            zNextStart = zFilletPoints[3];
         }
         else
         {
            OuterCurve.Append(Line.CreateBound(zNextStart, zShapePoints[7]));
            zNextStart = zShapePoints[7];
         }

         OuterCurve.Append(Line.CreateBound(zNextStart, zShapePoints[0]));
      }

      protected IFCParameterizedProfile()
      {

      }

      public static IFCParameterizedProfile CreateIFCParameterizedProfile(IfcParameterizedProfileDef ifcProfileDef)
      {
         if (ifcProfileDef == null)
         {
            Importer.TheLog.LogNullError(IFCEntityType.IfcProfileDef);
            return null;
         }
         IfcRectangleProfileDef rectangleProfileDef = ifcProfileDef as IfcRectangleProfileDef;
         if (rectangleProfileDef != null)
            return new IFCParameterizedProfile(rectangleProfileDef);
         IfcCircleProfileDef circleProfileDef = ifcProfileDef as IfcCircleProfileDef;
         if (circleProfileDef != null)
            return new IFCParameterizedProfile(circleProfileDef);
         IfcEllipseProfileDef ellipseProfileDef = ifcProfileDef as IfcEllipseProfileDef;
         if (ellipseProfileDef != null)
            return new IFCParameterizedProfile(ellipseProfileDef);
         IfcCShapeProfileDef cShapeProfileDef = ifcProfileDef as IfcCShapeProfileDef;
         if (cShapeProfileDef != null)
            return new IFCParameterizedProfile(cShapeProfileDef);
         IfcIShapeProfileDef iShapeProfileDef = ifcProfileDef as IfcIShapeProfileDef;
         if (iShapeProfileDef != null)
            return new IFCParameterizedProfile(iShapeProfileDef);
         IfcLShapeProfileDef lShapeProfileDef = ifcProfileDef as IfcLShapeProfileDef;
         if (lShapeProfileDef != null)
            return new IFCParameterizedProfile(lShapeProfileDef);
         IfcTShapeProfileDef tShapeProfileDef = ifcProfileDef as IfcTShapeProfileDef;
         if (tShapeProfileDef != null)
            return new IFCParameterizedProfile(tShapeProfileDef);
         IfcUShapeProfileDef uShapeProfileDef = ifcProfileDef as IfcUShapeProfileDef;
         if (uShapeProfileDef != null)
            return new IFCParameterizedProfile(uShapeProfileDef);
         IfcZShapeProfileDef zShapeProfileDef = ifcProfileDef as IfcZShapeProfileDef;
         if (zShapeProfileDef != null)
            return new IFCParameterizedProfile(zShapeProfileDef);

         Importer.TheLog.LogUnhandledSubTypeError(ifcProfileDef, IFCEntityType.IfcProfileDef, false);
         return null;
      }

   }

   /// <summary>
   /// Provides methods to process IfcProfileDef and its subclasses.
   /// </summary>
   public class IFCSimpleProfile : IFCProfileDef
   {
      private CurveLoop m_OuterCurve = null;

      private IList<CurveLoop> m_InnerCurves = null;

      // This is only valid for IFCParameterizedProfile.  We place it here to be at the same level as the CurveLoops,
      // so that they can be transformed in a consisent matter.
      private Transform m_Position = null;

      /// <summary>
      /// The location (origin and rotation) of the parametric profile.
      /// </summary>
      public Transform Position
      {
         get { return m_Position; }
         protected set { m_Position = value; }
      }

      protected IFCSimpleProfile(IfcProfileDef profileDef)
         : base(profileDef)
      {

      }
      internal IFCSimpleProfile(IfcArbitraryOpenProfileDef profileDef)
         : base(profileDef)
      {
         IfcBoundedCurve curveHnd = profileDef.Curve;
         if (curveHnd == null)
         {
            Importer.TheLog.LogNullError(IFCEntityType.IfcArbitraryOpenProfileDef);
            return;
         }

         CurveLoop profileCurveLoop = curveHnd.CurveLoop();
         if (profileCurveLoop == null)
         {
            Curve profileCurve = curveHnd.Curve();
            if (profileCurve != null)
            {
               profileCurveLoop = new CurveLoop();
               profileCurveLoop.Append(profileCurve);
            }
         }


         if (profileCurveLoop != null)
         {
            IfcCenterLineProfileDef centerLineProfileDef = profileDef as IfcCenterLineProfileDef;
            if (centerLineProfileDef != null)
            {

               double thickness = IFCUnitUtil.ScaleLength(centerLineProfileDef.Thickness);
               if (double.IsNaN(thickness))
               {
                  //LOG: ERROR: IfcCenterLineProfileDef has no thickness defined.
                  return;
               }

               Plane plane = null;
               try
               {
                  plane = profileCurveLoop.GetPlane();
               }
               catch
               {
                  //LOG: ERROR: Curve for IfcCenterLineProfileDef is non-planar.
                  return;
               }

               profileCurveLoop = null;
               try
               {
                  profileCurveLoop = CurveLoop.CreateViaThicken(profileCurveLoop, thickness, plane.Normal);
               }
               catch
               {
               }
            }
         }

         if (profileCurveLoop != null)
            OuterCurve = profileCurveLoop;
         else
         {
            //LOG: ERROR: Invalid outer curve in IfcArbitraryOpenProfileDef.
            return;
         }
      }

      // In certain cases, Revit can't handle unbounded circles and ellipses.  Create a CurveLoop with the curve split into two segments.
      private CurveLoop CreateCurveLoopFromUnboundedCyclicCurve(Curve innerCurve)
      {
         if (innerCurve == null)
            return null;

         if (!innerCurve.IsCyclic)
            return null;

         // Note that we don't disallow bound curves, as they could be bound but still closed.

         // We don't know how to handle anything other than circles or ellipses with a period of 2PI.
         double period = innerCurve.Period;
         if (!MathUtil.IsAlmostEqual(period, Math.PI * 2.0))
            return null;

         double startParam = innerCurve.IsBound ? innerCurve.GetEndParameter(0) : 0.0;
         double endParam = innerCurve.IsBound ? innerCurve.GetEndParameter(1) : period;

         // Not a closed curve.
         if (!MathUtil.IsAlmostEqual(endParam - startParam, period))
            return null;

         Curve firstCurve = innerCurve.Clone();
         if (firstCurve == null)
            return null;

         Curve secondCurve = innerCurve.Clone();
         if (secondCurve == null)
            return null;

         firstCurve.MakeBound(0, period / 2.0);
         secondCurve.MakeBound(period / 2.0, period);

         CurveLoop innerCurveLoop = new CurveLoop();
         innerCurveLoop.Append(firstCurve);
         innerCurveLoop.Append(secondCurve);
         return innerCurveLoop;
      }

      internal IFCSimpleProfile(IfcArbitraryClosedProfileDef profileDef)
         :base(profileDef)
      {
         IfcBoundedCurve curveHnd = profileDef.OuterCurve;
         if(curveHnd == null)
            return;

         CurveLoop outerCurveLoop = curveHnd.CurveLoop();

         // We need to convert outerIFCCurve into a CurveLoop with bound curves.  This is handled below (with possible errors logged).
         if (outerCurveLoop != null)
            OuterCurve = outerCurveLoop;
         else
         {
            Curve outerCurve = curveHnd.Curve();
            if (outerCurve == null)
               Importer.TheLog.LogError(profileDef.StepId, "Couldn't convert outer curve #" + curveHnd.StepId + " in IfcArbitraryClosedProfileDef.", true);
            else
            {
               OuterCurve = CreateCurveLoopFromUnboundedCyclicCurve(outerCurve);
               if (OuterCurve == null)
               {
                  if (outerCurve.IsBound)
                     Importer.TheLog.LogError(profileDef.StepId, "Outer curve #" + curveHnd.StepId + " in IfcArbitraryClosedProfileDef isn't closed and can't be used.", true);
                  else
                     Importer.TheLog.LogError(profileDef.StepId, "Couldn't split unbound outer curve #" + curveHnd.StepId + " in IfcArbitraryClosedProfileDef.", true);
               }
            }
         }

         IfcArbitraryProfileDefWithVoids arbitraryProfileDefWithVoids = profileDef as IfcArbitraryProfileDefWithVoids;
         if (arbitraryProfileDefWithVoids != null)
         {
            IList<IfcCurve> innerCurveHnds = arbitraryProfileDefWithVoids.InnerCurves;
            if (innerCurveHnds == null || innerCurveHnds.Count == 0)
            {
               Importer.TheLog.LogWarning(profileDef.StepId, "IfcArbitraryProfileDefWithVoids has no voids.", false);
               return;
            }

            ISet<IfcCurve> usedHandles = new HashSet<IfcCurve>();
            foreach (IfcCurve innerCurveHnd in innerCurveHnds)
            {
               if (innerCurveHnd == null)
               {
                  Importer.TheLog.LogWarning(profileDef.StepId, "Null or invalid inner curve handle in IfcArbitraryProfileDefWithVoids.", false);
                  continue;
               }

               if (usedHandles.Contains(innerCurveHnd))
               {
                  Importer.TheLog.LogWarning(profileDef.StepId, "Duplicate void #" + innerCurveHnd.StepId + " in IfcArbitraryProfileDefWithVoids, ignoring.", false);
                  continue;
               }

               // If any inner is the same as the outer, throw an exception.
               if (curveHnd.Equals(innerCurveHnd))
               {
                  Importer.TheLog.LogError(profileDef.StepId, "Inner curve loop #" + innerCurveHnd.StepId + " same as outer curve loop in IfcArbitraryProfileDefWithVoids.", true);
                  continue;
               }

               usedHandles.Add(innerCurveHnd);

               CurveLoop innerCurveLoop = innerCurveHnd.CurveLoop();

               // See if we have a closed curve instead.
               if (innerCurveLoop == null)
                  innerCurveLoop = CreateCurveLoopFromUnboundedCyclicCurve(innerCurveHnd.Curve());

               if (innerCurveLoop == null)
               {
                  //LOG: WARNING: Null or invalid inner curve in IfcArbitraryProfileDefWithVoids.
                  Importer.TheLog.LogWarning(profileDef.StepId, "Invalid inner curve #" + innerCurveHnd.StepId + " in IfcArbitraryProfileDefWithVoids.", false);
                  continue;
               }

               InnerCurves.Add(innerCurveLoop);
            }
         }
      }

      /// <summary>
      /// Default constructor.
      /// </summary>
      protected IFCSimpleProfile()
      {

      }

      

      /// <summary>
      /// Process an IFCAnyHandle corresponding to a simple profile.
      /// </summary>
      /// <param name="ifcProfileDef"></param>
      /// <returns>IFCSimpleProfile object.</returns>
      public static IFCSimpleProfile CreateIFCSimpleProfile(IfcProfileDef ifcProfileDef, CreateElementIfcCache cache)
      {
         if (ifcProfileDef == null)
         {
            Importer.TheLog.LogNullError(IFCEntityType.IfcProfileDef);
            return null;
         }

         IFCSimpleProfile simpleProfile = null;
         if (cache.Profiles.TryGetValue(ifcProfileDef.StepId, out simpleProfile))
            return simpleProfile;

         IfcArbitraryClosedProfileDef arbitraryClosedProfileDef = ifcProfileDef as IfcArbitraryClosedProfileDef;
         if (arbitraryClosedProfileDef != null)
            simpleProfile = new IFCSimpleProfile(arbitraryClosedProfileDef);
         else
         {
            IfcArbitraryOpenProfileDef arbitraryOpenProfileDef = ifcProfileDef as IfcArbitraryOpenProfileDef;
            if (arbitraryOpenProfileDef != null)
               simpleProfile = new IFCSimpleProfile(arbitraryOpenProfileDef);
            else
            {
               IfcParameterizedProfileDef parameterizedProfileDef = ifcProfileDef as IfcParameterizedProfileDef;
               if (parameterizedProfileDef != null)
                  simpleProfile = IFCParameterizedProfile.CreateIFCParameterizedProfile(parameterizedProfileDef);
            }
         }
         cache.Profiles[ifcProfileDef.StepId] = simpleProfile;
         return simpleProfile;
      }

      /// <summary>
      /// Get the outer curve loop.
      /// </summary>
      public CurveLoop OuterCurve
      {
         get { return m_OuterCurve; }
         protected set { m_OuterCurve = value; }
      }

      /// <summary>
      /// Get the list of inner curve loops.
      /// </summary>
      public IList<CurveLoop> InnerCurves
      {
         get
         {
            if (m_InnerCurves == null)
               m_InnerCurves = new List<CurveLoop>();
            return m_InnerCurves;
         }
      }
   }
}