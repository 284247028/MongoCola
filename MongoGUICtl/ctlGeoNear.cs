﻿using System;
using System.Diagnostics;
using System.Windows.Forms;
using Common;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoGUICtl.ClientTree;
using MongoUtility.Core;
using ResourceLib.UI;

namespace MongoGUICtl
{
    public partial class CtlGeoNear : UserControl
    {
        public CtlGeoNear()
        {
            InitializeComponent();
            NumGeoX.KeyPress += NumberTextBox.NumberText_KeyPress;
            NumGeoY.KeyPress += NumberTextBox.NumberText_KeyPress;
            NumDistanceMultiplier.KeyPress += NumberTextBox.NumberText_KeyPress;
            NumMaxDistance.KeyPress += NumberTextBox.NumberText_KeyPress;
        }

        private void lnkGeoNear_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://docs.mongodb.org/manual/reference/commands/#geoNear");
        }

        private void cmdGeoNear_Click(object sender, EventArgs e)
        {
            BsonDocument mGeoNearAs = null;
            bool IsHaystack = chkHaystack.Checked;
            try
            {
                if (IsHaystack)
                {
                    var geoSearchOption = new GeoHaystackSearchArgs();
                    geoSearchOption.MaxDistance = double.Parse(NumMaxDistance.Text);
                    geoSearchOption.Limit = (int)NumResultCount.Value;
                    geoSearchOption.Near = new XYPoint(double.Parse(NumGeoX.Text), double.Parse(NumGeoY.Text));
                    //geoSearch
                    mGeoNearAs = RuntimeMongoDbContext.GetCurrentCollection().GeoHaystackSearchAs<BsonDocument>(geoSearchOption).Response;
                }
                else
                {
                    var geoOption = new GeoNearArgs();
                    geoOption.DistanceMultiplier = double.Parse(NumDistanceMultiplier.Text);
                    geoOption.MaxDistance = double.Parse(NumMaxDistance.Text);
                    geoOption.Spherical = chkSpherical.Checked;
                    geoOption.Limit = (int)NumResultCount.Value;
                    geoOption.Near = new XYPoint(double.Parse(NumGeoX.Text), double.Parse(NumGeoY.Text));
                    //geoNear
                    mGeoNearAs = RuntimeMongoDbContext.GetCurrentCollection().GeoNearAs<BsonDocument>(geoOption).Response;
                }
            }
            catch (Exception ex)
            {
                Utility.ExceptionDeal(ex);
                return;
            }
            UiHelper.FillDataToTreeView("Result", trvGeoResult, mGeoNearAs);
            trvGeoResult.DatatreeView.Nodes[0].Expand();

        }

        /// <summary>
        ///     设置公里
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnKM_Click(object sender, EventArgs e)
        {
            NumDistanceMultiplier.Text = (1 / 6378.137).ToString();
        }

        /// <summary>
        ///     设置英里
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnMile_Click(object sender, EventArgs e)
        {
            NumDistanceMultiplier.Text = (1 / 3963.192).ToString();
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            MyMessageBox.ShowMessage("Convert", "About Convert",
                @"distance to radians: divide the distance by the radius of the sphere (e.g. the Earth) in the same units as the distance measurement.
radians to distance: multiply the rad ian measure by the radius of the sphere (e.g. the Earth) in the units system that you want to convert the distance to.
The radius of the Earth is approximately 3963.192 miles or 6378.137 kilometers.", true);
        }
    }
}