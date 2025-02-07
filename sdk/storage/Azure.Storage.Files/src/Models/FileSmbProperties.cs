﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Azure.Storage.Files.Models
{
    /// <summary>
    /// The SMB properties for a file.
    /// </summary>
    public struct FileSmbProperties : IEquatable<FileSmbProperties>
    {
        /// <summary>
        /// The file system attributes for this file.
        /// </summary>
        public NtfsFileAttributes? FileAttributes { get; set; }

        /// <summary>
        /// The key of the file permission.
        /// </summary>
        public string FilePermissionKey { get; set; }

        /// <summary>
        /// The creation time of the file.
        /// </summary>
        public DateTimeOffset? FileCreationTime { get; set; }

        /// <summary>
        /// The last write time of the file.
        /// </summary>
        public DateTimeOffset? FileLastWriteTime { get; set; }

        /// <summary>
        /// The change time of the file.
        /// </summary>
        public DateTimeOffset? FileChangeTime { get; internal set; }

        /// <summary>
        /// The fileId of the file.
        /// </summary>
        public string FileId { get; internal set; }

        /// <summary>
        /// The parentId of the file
        /// </summary>
        public string ParentId { get; internal set; }

        internal FileSmbProperties(RawStorageFileInfo rawStorageFileInfo)
        {
            FileAttributes = NtfsFileAttributes.Parse(rawStorageFileInfo.FileAttributes);
            FilePermissionKey = rawStorageFileInfo.FilePermissionKey;
            FileCreationTime = rawStorageFileInfo.FileCreationTime;
            FileLastWriteTime = rawStorageFileInfo.FileLastWriteTime;
            FileChangeTime = rawStorageFileInfo.FileChangeTime;
            FileId = rawStorageFileInfo.FileId;
            ParentId = rawStorageFileInfo.FileParentId;

        }

        internal FileSmbProperties(RawStorageFileProperties rawStorageFileProperties)
        {
            FileAttributes = NtfsFileAttributes.Parse(rawStorageFileProperties.FileAttributes);
            FilePermissionKey = rawStorageFileProperties.FilePermissionKey;
            FileCreationTime = rawStorageFileProperties.FileCreationTime;
            FileLastWriteTime = rawStorageFileProperties.FileLastWriteTime;
            FileChangeTime = rawStorageFileProperties.FileChangeTime;
            FileId = rawStorageFileProperties.FileId;
            ParentId = rawStorageFileProperties.FileParentId;
        }

        internal FileSmbProperties(FlattenedStorageFileProperties flattenedStorageFileProperties)
        {
            FileAttributes = NtfsFileAttributes.Parse(flattenedStorageFileProperties.FileAttributes);
            FilePermissionKey = flattenedStorageFileProperties.FilePermissionKey;
            FileCreationTime = flattenedStorageFileProperties.FileCreationTime;
            FileLastWriteTime = flattenedStorageFileProperties.FileLastWriteTime;
            FileChangeTime = flattenedStorageFileProperties.FileChangeTime;
            FileId = flattenedStorageFileProperties.FileId;
            ParentId = flattenedStorageFileProperties.FileParentId;
        }

        internal FileSmbProperties(RawStorageDirectoryInfo rawStorageDirectoryInfo)
        {
            FileAttributes = NtfsFileAttributes.Parse(rawStorageDirectoryInfo.FileAttributes);
            FilePermissionKey = rawStorageDirectoryInfo.FilePermissionKey;
            FileCreationTime = rawStorageDirectoryInfo.FileCreationTime;
            FileLastWriteTime = rawStorageDirectoryInfo.FileLastWriteTime;
            FileChangeTime = rawStorageDirectoryInfo.FileChangeTime;
            FileId = rawStorageDirectoryInfo.FileId;
            ParentId = rawStorageDirectoryInfo.FileParentId;
        }

        internal FileSmbProperties(RawStorageDirectoryProperties rawStorageDirectoryProperties)
        {
            FileAttributes = NtfsFileAttributes.Parse(rawStorageDirectoryProperties.FileAttributes);
            FilePermissionKey = rawStorageDirectoryProperties.FilePermissionKey;
            FileCreationTime = rawStorageDirectoryProperties.FileCreationTime;
            FileLastWriteTime = rawStorageDirectoryProperties.FileLastWriteTime;
            FileChangeTime = rawStorageDirectoryProperties.FileChangeTime;
            FileId = rawStorageDirectoryProperties.FileId;
            ParentId = rawStorageDirectoryProperties.FileParentId;
        }

        /// <summary>
        /// Checks if two FileSmbProperties are equal.
        /// </summary>
        /// <param name="other">The other instance to compare to.</param>
        /// <returns></returns>
        public override bool Equals(object other)
            => other is FileSmbProperties props && Equals(props);

        /// <summary>
        /// Gets the hash code for the FileSmbProperties.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
            => FileAttributes.GetHashCode()
            ^ FilePermissionKey.GetHashCode()
            ^ FileCreationTime.GetHashCode()
            ^ FileLastWriteTime.GetHashCode()
            ^ FileChangeTime.GetHashCode()
            ^ FileId.GetHashCode()
            ^ ParentId.GetHashCode();

        /// <summary>
        /// Check if two FileSmbProperties instances are equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>True if they're equal, false otherwise.</returns>
        public static bool operator ==(FileSmbProperties left, FileSmbProperties right) => left.Equals(right);

        /// <summary>
        /// Check if two FileSmbProperties instances are not equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>True if they're not equal, false otherwise.</returns>
        public static bool operator !=(FileSmbProperties left, FileSmbProperties right) => !(left == right);

        /// <summary>
        /// Check if two FileSmbProperties instances are equal.
        /// </summary>
        /// <param name="other">The other instance to compare to.</param>
        public bool Equals(FileSmbProperties other)
            => FileAttributes == other.FileAttributes
            && FilePermissionKey == other.FilePermissionKey
            && FileCreationTime == other.FileCreationTime
            && FileLastWriteTime == other.FileLastWriteTime
            && FileChangeTime == other.FileChangeTime
            && FileId == other.FileId
            && ParentId == other.ParentId;

        internal string FileCreationTimeToString()
            => NullableDateTimeOffsetToString(FileCreationTime);

        internal string FileLastWriteTimeToString()
            => NullableDateTimeOffsetToString(FileLastWriteTime);

        private static string NullableDateTimeOffsetToString(DateTimeOffset? dateTimeOffset)
            => dateTimeOffset.HasValue ? DateTimeOffSetToString(dateTimeOffset.Value) : null;

        private static string DateTimeOffSetToString(DateTimeOffset dateTimeOffset)
            => dateTimeOffset.UtcDateTime.ToString(Constants.File.FileTimeFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// FilesModelFactory provides utilities for mocking.
    /// </summary>
    public static partial class FilesModelFactory
    {
        /// <summary>
        /// Creates a new FileSmbProperties instance for mocking.
        /// </summary>
        public static FileSmbProperties FileSmbProperties(
            DateTimeOffset? fileChangeTime,
            string fileId,
            string parentId) => new FileSmbProperties
            {
                FileChangeTime = fileChangeTime,
                FileId = fileId,
                ParentId = parentId
            };
    }
}
