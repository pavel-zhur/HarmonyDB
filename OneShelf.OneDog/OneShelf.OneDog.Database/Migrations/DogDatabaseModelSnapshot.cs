﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OneShelf.OneDog.Database;

#nullable disable

namespace OneShelf.OneDog.Database.Migrations
{
    [DbContext(typeof(DogDatabase))]
    partial class DogDatabaseModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("DomainUser", b =>
                {
                    b.Property<int>("AdministratedDomainsId")
                        .HasColumnType("int");

                    b.Property<long>("AdministratorsId")
                        .HasColumnType("bigint");

                    b.HasKey("AdministratedDomainsId", "AdministratorsId");

                    b.HasIndex("AdministratorsId");

                    b.ToTable("DomainUser");
                });

            modelBuilder.Entity("OneShelf.OneDog.Database.Model.Chat", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<long>("ChatId")
                        .HasColumnType("bigint");

                    b.Property<int>("DomainId")
                        .HasColumnType("int");

                    b.Property<DateTime>("FirstUpdateReceivedOn")
                        .HasColumnType("datetime2");

                    b.Property<bool?>("IsForum")
                        .HasColumnType("bit");

                    b.Property<DateTime>("LastUpdateReceivedOn")
                        .HasColumnType("datetime2");

                    b.Property<string>("Title")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("UpdatesCount")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("ChatId");

                    b.HasIndex("DomainId", "ChatId")
                        .IsUnique();

                    b.ToTable("Chats");
                });

            modelBuilder.Entity("OneShelf.OneDog.Database.Model.Domain", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<float?>("BillingRatio")
                        .HasColumnType("real");

                    b.Property<string>("BotToken")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<long>("ChatId")
                        .HasColumnType("bigint");

                    b.Property<int>("DalleVersion")
                        .HasColumnType("int");

                    b.Property<float?>("FrequencyPenalty")
                        .HasColumnType("real");

                    b.Property<string>("GptVersion")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("bit");

                    b.Property<string>("LyriaModel")
                        .HasColumnType("nvarchar(max)");

                    b.Property<float?>("PresencePenalty")
                        .HasColumnType("real");

                    b.Property<string>("PrivateDescription")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SoraModel")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SystemMessage")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("TopicId")
                        .HasColumnType("int");

                    b.Property<string>("WebHooksSecretToken")
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("WebHooksSecretToken");

                    b.ToTable("Domains");
                });

            modelBuilder.Entity("OneShelf.OneDog.Database.Model.Interaction", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("CreatedOn")
                        .HasColumnType("datetime2");

                    b.Property<int>("DomainId")
                        .HasColumnType("int");

                    b.Property<string>("InteractionType")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Serialized")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ShortInfoSerialized")
                        .HasColumnType("nvarchar(max)");

                    b.Property<long>("UserId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("DomainId");

                    b.HasIndex("UserId");

                    b.HasIndex("InteractionType", "DomainId", "CreatedOn");

                    b.ToTable("Interactions");
                });

            modelBuilder.Entity("OneShelf.OneDog.Database.Model.User", b =>
                {
                    b.Property<long>("Id")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("CreatedOn")
                        .HasColumnType("datetime2");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("DomainUser", b =>
                {
                    b.HasOne("OneShelf.OneDog.Database.Model.Domain", null)
                        .WithMany()
                        .HasForeignKey("AdministratedDomainsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("OneShelf.OneDog.Database.Model.User", null)
                        .WithMany()
                        .HasForeignKey("AdministratorsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("OneShelf.OneDog.Database.Model.Chat", b =>
                {
                    b.HasOne("OneShelf.OneDog.Database.Model.Domain", "Domain")
                        .WithMany("Chats")
                        .HasForeignKey("DomainId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Domain");
                });

            modelBuilder.Entity("OneShelf.OneDog.Database.Model.Domain", b =>
                {
                    b.OwnsOne("OneShelf.OneDog.Database.Model.MediaLimit", "ImagesLimit", b1 =>
                        {
                            b1.Property<int>("DomainId")
                                .HasColumnType("int");

                            b1.Property<int>("Limit")
                                .HasColumnType("int");

                            b1.Property<long>("Window")
                                .HasColumnType("bigint");

                            b1.HasKey("DomainId");

                            b1.ToTable("Domains");

                            b1.WithOwner()
                                .HasForeignKey("DomainId");
                        });

                    b.OwnsOne("OneShelf.OneDog.Database.Model.MediaLimit", "MusicLimit", b1 =>
                        {
                            b1.Property<int>("DomainId")
                                .HasColumnType("int");

                            b1.Property<int>("Limit")
                                .HasColumnType("int");

                            b1.Property<long>("Window")
                                .HasColumnType("bigint");

                            b1.HasKey("DomainId");

                            b1.ToTable("Domains");

                            b1.WithOwner()
                                .HasForeignKey("DomainId");
                        });

                    b.OwnsOne("OneShelf.OneDog.Database.Model.MediaLimit", "VideosLimit", b1 =>
                        {
                            b1.Property<int>("DomainId")
                                .HasColumnType("int");

                            b1.Property<int>("Limit")
                                .HasColumnType("int");

                            b1.Property<long>("Window")
                                .HasColumnType("bigint");

                            b1.HasKey("DomainId");

                            b1.ToTable("Domains");

                            b1.WithOwner()
                                .HasForeignKey("DomainId");
                        });

                    b.Navigation("ImagesLimit");

                    b.Navigation("MusicLimit");

                    b.Navigation("VideosLimit");
                });

            modelBuilder.Entity("OneShelf.OneDog.Database.Model.Interaction", b =>
                {
                    b.HasOne("OneShelf.OneDog.Database.Model.Domain", "Domain")
                        .WithMany()
                        .HasForeignKey("DomainId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("OneShelf.OneDog.Database.Model.User", "User")
                        .WithMany("Interactions")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Domain");

                    b.Navigation("User");
                });

            modelBuilder.Entity("OneShelf.OneDog.Database.Model.Domain", b =>
                {
                    b.Navigation("Chats");
                });

            modelBuilder.Entity("OneShelf.OneDog.Database.Model.User", b =>
                {
                    b.Navigation("Interactions");
                });
#pragma warning restore 612, 618
        }
    }
}
