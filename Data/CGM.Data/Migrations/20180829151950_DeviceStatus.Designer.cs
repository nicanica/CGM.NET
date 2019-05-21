﻿// <auto-generated />
using CGM.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CGM.Data.Migrations
{
    [DbContext(typeof(CgmContext))]
    [Migration("20180829151950_DeviceStatus")]
    partial class DeviceStatus
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.1.1-rtm-30846");

            modelBuilder.Entity("CGM.Data.Model.Configuration", b =>
                {
                    b.Property<string>("ConfigurationName")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ConfigurationValue");

                    b.HasKey("ConfigurationName");

                    b.ToTable("Configuration");
                });

            modelBuilder.Entity("CGM.Data.Model.Device", b =>
                {
                    b.Property<string>("SerialNumber")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("LinkKey");

                    b.Property<string>("LinkMac");

                    b.Property<string>("Name");

                    b.Property<string>("PumpMac");

                    b.Property<string>("RadioChannel");

                    b.Property<string>("SerialNumberFull");

                    b.HasKey("SerialNumber");

                    b.ToTable("Device");
                });

            modelBuilder.Entity("CGM.Data.Model.DeviceStatus", b =>
                {
                    b.Property<string>("DeviceStatusKey")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("DeviceStatusBytes");

                    b.HasKey("DeviceStatusKey");

                    b.ToTable("DeviceStatus");
                });

            modelBuilder.Entity("CGM.Data.Model.History", b =>
                {
                    b.Property<string>("Key")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("EventType");

                    b.Property<string>("HistoryBytes");

                    b.Property<int>("HistoryDataType");

                    b.Property<int>("Rtc");

                    b.HasKey("Key");

                    b.ToTable("History");
                });

            modelBuilder.Entity("CGM.Data.Model.HistoryStatus", b =>
                {
                    b.Property<string>("Key")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Comment");

                    b.Property<int>("Status");

                    b.HasKey("Key");

                    b.ToTable("HistoryStatus");
                });
#pragma warning restore 612, 618
        }
    }
}
