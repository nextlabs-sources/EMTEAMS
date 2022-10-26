﻿using System;
using System.Linq;
using System.Text;
#if !NETSTANDARD2_0
using System.Web.UI;
#endif

namespace OfficeDevPnP.Core.Pages
{
#if !SP2013 && !SP2016
    /// <summary>
    /// Represents a section on the canvas
    /// </summary>
    public class CanvasSection
    {
        #region variables
        private System.Collections.Generic.List<CanvasColumn> columns = new System.Collections.Generic.List<CanvasColumn>(3);
        private ClientSidePage page;
        private int zoneEmphasis;
        #endregion

        #region construction
        internal CanvasSection(ClientSidePage page)
        {
            if (page == null)
            {
                throw new ArgumentNullException("Passed page cannot be null");
            }

            this.page = page;
            this.zoneEmphasis = 0;
            Order = 0;
        }

        /// <summary>
        /// Creates a new canvas section
        /// </summary>
        /// <param name="page"><see cref="ClientSidePage"/> instance that holds this section</param>
        /// <param name="canvasSectionTemplate">Type of section to create</param>
        /// <param name="order">Order of this section in the collection of sections on the page</param>
        public CanvasSection(ClientSidePage page, CanvasSectionTemplate canvasSectionTemplate, float order)
        {
            if (page == null)
            {
                throw new ArgumentNullException("Passed page cannot be null");
            }

            this.page = page;
            this.zoneEmphasis = 0;
            Type = canvasSectionTemplate;
            Order = order;

            switch (canvasSectionTemplate)
            {
                case CanvasSectionTemplate.OneColumn:
                    goto default;
                case CanvasSectionTemplate.OneColumnFullWidth:
                    this.columns.Add(new CanvasColumn(this, 1, 0));
                    break;
#if !SP2019
                case CanvasSectionTemplate.OneColumnVerticalSection:
                    this.columns.Add(new CanvasColumn(this, 1, 0, 1));
                    this.columns.Add(new CanvasColumn(this, 1, 12, 2));
                    break;
#endif
                case CanvasSectionTemplate.TwoColumn:
                    this.columns.Add(new CanvasColumn(this, 1, 6));
                    this.columns.Add(new CanvasColumn(this, 2, 6));
                    break;
#if !SP2019
                case CanvasSectionTemplate.TwoColumnVerticalSection:
                    this.columns.Add(new CanvasColumn(this, 1, 6, 1));
                    this.columns.Add(new CanvasColumn(this, 2, 6, 1));
                    this.columns.Add(new CanvasColumn(this, 1, 12, 2));
                    break;
#endif
                case CanvasSectionTemplate.ThreeColumn:
                    this.columns.Add(new CanvasColumn(this, 1, 4));
                    this.columns.Add(new CanvasColumn(this, 2, 4));
                    this.columns.Add(new CanvasColumn(this, 3, 4));
                    break;
#if !SP2019
                case CanvasSectionTemplate.ThreeColumnVerticalSection:
                    this.columns.Add(new CanvasColumn(this, 1, 4, 1));
                    this.columns.Add(new CanvasColumn(this, 2, 4, 1));
                    this.columns.Add(new CanvasColumn(this, 3, 4, 1));
                    this.columns.Add(new CanvasColumn(this, 1, 12, 2));
                    break;
#endif
                case CanvasSectionTemplate.TwoColumnLeft:
                    this.columns.Add(new CanvasColumn(this, 1, 8));
                    this.columns.Add(new CanvasColumn(this, 2, 4));
                    break;
#if !SP2019
                case CanvasSectionTemplate.TwoColumnLeftVerticalSection:
                    this.columns.Add(new CanvasColumn(this, 1, 8, 1));
                    this.columns.Add(new CanvasColumn(this, 2, 4, 1));
                    this.columns.Add(new CanvasColumn(this, 1, 12, 2));
                    break;
#endif
                case CanvasSectionTemplate.TwoColumnRight:
                    this.columns.Add(new CanvasColumn(this, 1, 4));
                    this.columns.Add(new CanvasColumn(this, 2, 8));
                    break;
#if !SP2019
                case CanvasSectionTemplate.TwoColumnRightVerticalSection:
                    this.columns.Add(new CanvasColumn(this, 1, 4, 1));
                    this.columns.Add(new CanvasColumn(this, 2, 8, 1));
                    this.columns.Add(new CanvasColumn(this, 1, 12, 2));
                    break;
#endif
                default:
                    this.columns.Add(new CanvasColumn(this, 1, 12));
                    break;
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// Type of the section
        /// </summary>
        public CanvasSectionTemplate Type { get; set; }

        /// <summary>
        /// Order in which this section is presented on the page
        /// </summary>
        public float Order { get; set; }

        /// <summary>
        /// <see cref="CanvasColumn"/> instances that are part of this section
        /// </summary>
        public System.Collections.Generic.List<CanvasColumn> Columns
        {
            get
            {
                return this.columns;
            }
        }

        /// <summary>
        /// The <see cref="ClientSidePage"/> instance holding this section
        /// </summary>
        public ClientSidePage Page
        {
            get
            {
                return this.page;
            }
        }

        /// <summary>
        /// Controls hosted in this section
        /// </summary>
        public System.Collections.Generic.List<CanvasControl> Controls
        {
            get
            {
                return this.Page.Controls.Where(p => p.Section == this).ToList<CanvasControl>();
            }
        }

        /// <summary>
        /// The default <see cref="CanvasColumn"/> of this section
        /// </summary>
        public CanvasColumn DefaultColumn
        {
            get
            {
                if (this.columns.Count == 0)
                {
                    this.columns.Add(new CanvasColumn(this));
                }

                return this.columns.First();
            }
        }

        /// <summary>
        /// A page can contain one section that has a vertical section column...use this attribute to get that column
        /// </summary>
        public CanvasColumn VerticalSectionColumn
        {
            get
            {
                return this.columns.Where(p => p.LayoutIndex == 2).FirstOrDefault();                
            }
        }

        /// <summary>
        /// Color emphasis of the section 
        /// </summary>
        public int ZoneEmphasis
        {
            get
            {
                return this.zoneEmphasis;
            }
            set
            {
                if (value < 0 || value > 3)
                {
                    throw new ArgumentException($"The zoneEmphasis value needs to be between 0 and 3. See the Microsoft.SharePoint.Client.SPVariantThemeType values for the why.");
                }

                this.zoneEmphasis = value;
            }
        }

        #endregion

        #region public methods
        /// <summary>
        /// Renders this section as a HTML fragment
        /// </summary>
        /// <returns>HTML string representing this section</returns>
        public string ToHtml()
        {
            StringBuilder html = new StringBuilder(100);
#if !NETSTANDARD2_0
            using (var htmlWriter = new HtmlTextWriter(new System.IO.StringWriter(html), ""))
            {
                htmlWriter.NewLine = string.Empty;
#endif
                foreach (var column in this.columns.OrderBy(z => z.LayoutIndex).ThenBy(z => z.Order))
                {
#if NETSTANDARD2_0
                    html.Append(column.ToHtml());
#else
                    htmlWriter.Write(column.ToHtml());
#endif
                }
#if !NETSTANDARD2_0
            }
#endif
            return html.ToString();
        }
        #endregion

        #region internal and private methods
        internal void AddColumn(CanvasColumn column)
        {
            if (column == null)
            {
                throw new ArgumentNullException("Passed column cannot be null");
            }

            this.columns.Add(column);
        }

        internal void MergeVerticalSectionColumn(CanvasColumn column)
        {
            // What was the highest order
            int order = 1;
            var lastColumn = this.columns.OrderBy(p => p.Order).FirstOrDefault();
            if (lastColumn != null)
            {
                order = lastColumn.Order + 1;
            }

            // Add the column to this section, first ensure it's connected to the new section and it's order has been updated for insertion in the new section
            column.MoveTo(this);
            column.Order = order;

            this.AddColumn(column);            
        }

        #endregion
    }
#endif
            }
