using System;
using System.Collections.Generic;

namespace ChromiumUpdate
{
    public struct ObjectMetadata
    {
        public string kind, id, name, bucket,contentType;
        public long size, generation, metageneration;
        public DateTime timeCreated, updated, timeStorageClassUpdated;
        public Uri mediaLink, selfLink;
        public byte[] md5hash, crc32c, etag;
        public Dictionary<string, string> metadata;
    }
}
