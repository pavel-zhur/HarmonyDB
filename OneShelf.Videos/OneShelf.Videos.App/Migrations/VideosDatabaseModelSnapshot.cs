﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OneShelf.Videos.App.Database;

#nullable disable

namespace OneShelf.Videos.App.Migrations.VideosDatabaseMigrations
{
    [DbContext(typeof(VideosDatabase))]
    partial class VideosDatabaseModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("OneShelf.Videos.App.Database.Models.UploadedItem", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<long>("ChatId")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedOn")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("FileNameTimestamp")
                        .HasColumnType("datetime2");

                    b.Property<string>("Json")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MediaItemId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool?>("MediaItemIsPhoto")
                        .HasColumnType("bit");

                    b.Property<bool?>("MediaItemIsVideo")
                        .HasColumnType("bit");

                    b.Property<DateTime?>("MediaItemMetadataCreationType")
                        .HasColumnType("datetime2");

                    b.Property<string>("MediaItemMimeType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("MediaItemSyncDate")
                        .HasColumnType("datetime2");

                    b.Property<int>("MessageId")
                        .HasColumnType("int");

                    b.Property<string>("Status")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("StatusCode")
                        .HasColumnType("int");

                    b.Property<string>("StatusMessage")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("TelegramPublishedOn")
                        .HasColumnType("datetime2");

                    b.HasKey("Id");

                    b.ToTable("UploadedItems");
                });
#pragma warning restore 612, 618
        }
    }
}