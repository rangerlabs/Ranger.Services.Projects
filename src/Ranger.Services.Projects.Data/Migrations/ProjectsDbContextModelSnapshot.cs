﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects.Data.Migrations
{
    [DbContext(typeof(ProjectsDbContext))]
    partial class ProjectsDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn)
                .HasAnnotation("ProductVersion", "2.2.4-servicing-10062")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("id");

                    b.Property<string>("FriendlyName")
                        .HasColumnName("friendly_name");

                    b.Property<string>("Xml")
                        .HasColumnName("xml");

                    b.HasKey("Id")
                        .HasName("pk_data_protection_keys");

                    b.ToTable("data_protection_keys");
                });

            modelBuilder.Entity("Ranger.Services.Projects.Data.ProjectStream", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("id");

                    b.Property<Guid>("ApiKey")
                        .HasColumnName("api_key");

                    b.Property<string>("DatabaseUsername")
                        .IsRequired()
                        .HasColumnName("database_username");

                    b.Property<string>("Domain")
                        .IsRequired()
                        .HasColumnName("domain")
                        .HasMaxLength(28);

                    b.Property<string>("Event")
                        .IsRequired()
                        .HasColumnName("event");

                    b.Property<DateTime>("InsertedAt")
                        .HasColumnName("inserted_at");

                    b.Property<string>("InsertedBy")
                        .IsRequired()
                        .HasColumnName("inserted_by");

                    b.Property<string>("ProjectData")
                        .IsRequired()
                        .HasColumnName("project_data");

                    b.Property<Guid>("ProjectId")
                        .HasColumnName("project_id");

                    b.Property<Guid>("StreamId")
                        .HasColumnName("stream_id");

                    b.Property<int>("Version")
                        .HasColumnName("version");

                    b.HasKey("Id")
                        .HasName("pk_project_streams");

                    b.HasIndex("Domain", "ProjectId")
                        .IsUnique();

                    b.HasIndex("Domain", "Version")
                        .IsUnique();

                    b.ToTable("project_streams");
                });
#pragma warning restore 612, 618
        }
    }
}
