﻿using Hashgraph.Test.Fixtures;
using System;
using System.Linq;
using Xunit;

namespace Hashgraph.Tests
{
    public class EndorsementsTests
    {
        [Fact(DisplayName = "Endorsements: Can Create Valid Endorsements Object")]
        public void CreateValidEndorsementsObject()
        {
            var (publicKey1, _) = Generator.KeyPair();
            var (publicKey2, _) = Generator.KeyPair();

            new Endorsement(publicKey1);
            new Endorsement(1, publicKey1);
            new Endorsement(publicKey1, publicKey2);
            new Endorsement(1, new Endorsement(1, publicKey1, publicKey2), new Endorsement(2, publicKey1, publicKey2));
        }
        [Fact(DisplayName = "Endorsements: Too large of a requried count throws error.")]
        public void TooLargeRequiredCountThrowsError()
        {

            var (publicKey, _) = Generator.KeyPair();
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new Endorsement((uint)Generator.Integer(2, 4), publicKey);
            });
            Assert.Equal("requiredCount", exception.ParamName);
            Assert.StartsWith("The required number of keys for a valid signature cannot exceed the number of public keys provided.", exception.Message);
        }
        [Fact(DisplayName = "Endorsements: Empty Private key throws Exception")]
        public void EmptyValueForKeyThrowsError()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new Endorsement();
            });
            Assert.Equal("endorsements", exception.ParamName);
            Assert.StartsWith("At least one endorsement in a list is required.", exception.Message);
        }
        [Fact(DisplayName = "Endorsements: Invalid Bytes in Private key throws Exception")]
        public void InvalidBytesForValueForKeyThrowsError()
        {
            var (originalKey, _) = Generator.KeyPair();
            var invalidKey = originalKey.ToArray();
            invalidKey[0] = 0;
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new Endorsement(KeyType.Ed25519, invalidKey);
            });
            Assert.StartsWith("The public key was not provided in a recognizable Ed25519 format.", exception.Message);
        }
        [Fact(DisplayName = "Endorsements: Invalid Byte Length in Private key throws Exception")]
        public void InvalidByteLengthForValueForKeyThrowsError()
        {
            var (originalKey, _) = Generator.KeyPair();
            var invalidKey = originalKey.ToArray().Take(32).ToArray();
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new Endorsement(KeyType.Ed25519, invalidKey);
            });
            Assert.StartsWith("The public key was not provided in a recognizable Ed25519 format.", exception.Message);
        }
        [Fact(DisplayName = "Endorsements: Equivalent Endorsements are considered Equal")]
        public void EquivalentEndorsementsAreConsideredEqual()
        {
            var (publicKey1, _) = Generator.KeyPair();
            var (publicKey2, _) = Generator.KeyPair();
            var endorsement1 = new Endorsement(publicKey1);
            var endorsement2 = new Endorsement(publicKey1);
            Assert.Equal(endorsement1, endorsement2);
            Assert.True(endorsement1 == endorsement2);
            Assert.False(endorsement1 != endorsement2);

            endorsement1 = new Endorsement(publicKey1, publicKey2);
            endorsement2 = new Endorsement(publicKey1, publicKey2);
            Assert.Equal(endorsement1, endorsement2);
            Assert.True(endorsement1 == endorsement2);
            Assert.False(endorsement1 != endorsement2);

            object asObject1 = endorsement1;
            object asObject2 = endorsement2;
            Assert.Equal(asObject1, asObject2);
            Assert.True(endorsement1.Equals(asObject1));
            Assert.True(asObject1.Equals(endorsement1));
        }
        [Fact(DisplayName = "Endorsements: Disimilar Endorsements are not considered Equal")]
        public void DisimilarEndorsementsAreNotConsideredEqual()
        {
            var (publicKey1, _) = Generator.KeyPair();
            var (publicKey2, _) = Generator.KeyPair();
            var endorsements1 = new Endorsement(publicKey1);
            var endorsements2 = new Endorsement(publicKey2);
            Assert.NotEqual(endorsements1, endorsements2);
            Assert.False(endorsements1 == endorsements2);
            Assert.True(endorsements1 != endorsements2);

            endorsements1 = new Endorsement(publicKey1);
            endorsements2 = new Endorsement(publicKey1, publicKey2);
            Assert.NotEqual(endorsements1, endorsements2);
            Assert.False(endorsements1 == endorsements2);
            Assert.True(endorsements1 != endorsements2);

            endorsements1 = new Endorsement(publicKey1, publicKey2);
            endorsements2 = new Endorsement(1, publicKey1, publicKey2);
            Assert.NotEqual(endorsements1, endorsements2);
            Assert.False(endorsements1 == endorsements2);
            Assert.True(endorsements1 != endorsements2);
        }

        [Fact(DisplayName = "Endorsements: Disimilar Multi-Key Endorsements are not considered Equal")]
        public void DisimilarMultiKeyEndorsementsAreNotConsideredEqual()
        {
            var (publicKey1, _) = Generator.KeyPair();
            var (publicKey2, _) = Generator.KeyPair();
            var (publicKey3, _) = Generator.KeyPair();
            var endorsements1 = new Endorsement(publicKey1, publicKey2);
            var endorsements2 = new Endorsement(publicKey2, publicKey3);
            Assert.NotEqual(endorsements1, endorsements2);
            Assert.False(endorsements1 == endorsements2);
            Assert.True(endorsements1 != endorsements2);

            endorsements1 = new Endorsement(1, publicKey1, publicKey2);
            endorsements2 = new Endorsement(2, publicKey2, publicKey3);
            Assert.NotEqual(endorsements1, endorsements2);
            Assert.False(endorsements1 == endorsements2);
            Assert.True(endorsements1 != endorsements2);

            endorsements1 = new Endorsement(1, publicKey1, publicKey2, publicKey3);
            endorsements2 = new Endorsement(2, publicKey1, publicKey2, publicKey3);
            Assert.NotEqual(endorsements1, endorsements2);
            Assert.False(endorsements1 == endorsements2);
            Assert.True(endorsements1 != endorsements2);

            endorsements1 = new Endorsement(2, publicKey1, publicKey2, publicKey3);
            endorsements2 = new Endorsement(3, publicKey1, publicKey2, publicKey3);
            Assert.NotEqual(endorsements1, endorsements2);
            Assert.False(endorsements1 == endorsements2);
            Assert.True(endorsements1 != endorsements2);
        }
        [Fact(DisplayName = "Endorsements: Default Creates Ed25519 Type")]
        public void CreateWithDefaultConsturctorProducesEd25519KeyType()
        {
            var (publicKey1, _) = Generator.KeyPair();

            var endorsement = new Endorsement(publicKey1);
            Assert.Equal(KeyType.Ed25519, endorsement.Type);
            Assert.Empty(endorsement.List);
            Assert.Equal(0U, endorsement.RequiredCount);
            Assert.Equal(publicKey1.ToArray(), endorsement.PublicKey.ToArray());
        }
        [Fact(DisplayName = "Endorsements: Creating Ed25519 Type produces Ed25519 type")]
        public void CanCreateEd25519Type()
        {
            var (publicKey1, _) = Generator.KeyPair();

            var endorsement = new Endorsement(KeyType.Ed25519, publicKey1);
            Assert.Equal(KeyType.Ed25519, endorsement.Type);
            Assert.Empty(endorsement.List);
            Assert.Equal(0U, endorsement.RequiredCount);
            Assert.Equal(publicKey1.ToArray(), endorsement.PublicKey.ToArray());
        }
        [Fact(DisplayName = "Endorsements: Creating RSA3072 Type produces RSA3072 type")]
        public void CanCreateRSA3072Type()
        {
            var (publicKey1, _) = Generator.KeyPair();

            var endorsement = new Endorsement(KeyType.RSA3072, publicKey1);
            Assert.Equal(KeyType.RSA3072, endorsement.Type);
            Assert.Empty(endorsement.List);
            Assert.Equal(0U, endorsement.RequiredCount);
            Assert.Equal(publicKey1.ToArray(), endorsement.PublicKey.ToArray());
        }
        [Fact(DisplayName = "Endorsements: Creating ECDSA384 Type produces RSA3072 type")]
        public void CanCreateECDSA384Type()
        {
            var (publicKey1, _) = Generator.KeyPair();

            var endorsement = new Endorsement(KeyType.ECDSA384, publicKey1);
            Assert.Equal(KeyType.ECDSA384, endorsement.Type);
            Assert.Empty(endorsement.List);
            Assert.Equal(0U, endorsement.RequiredCount);
            Assert.Equal(publicKey1.ToArray(), endorsement.PublicKey.ToArray());
        }
        [Fact(DisplayName = "Endorsements: Creating n of m List produces n of m List type")]
        public void CanCreateNofMList()
        {
            var n = (uint)Generator.Integer(1, 4);
            var m = Generator.Integer(5, 10);
            var keys = Enumerable.Range(0, m).Select(i => new Endorsement(Generator.KeyPair().publicKey)).ToArray();
            var list = new Endorsement(n, keys);
            Assert.Equal(KeyType.List, list.Type);
            Assert.Equal(n, list.RequiredCount);
            Assert.Equal(m, list.List.Length);
            for (int i = 0; i < m; i++)
            {
                Assert.Equal(keys[i], list.List[i]);
            }
        }
        [Fact(DisplayName = "Endorsements: Can Enumerte a Tree")]
        public void CanEnumrateAnEndorsementTree()
        {
            var (publicKey1a, _) = Generator.KeyPair();
            var (publicKey2a, _) = Generator.KeyPair();
            var (publicKey3a, _) = Generator.KeyPair();
            var (publicKey1b, _) = Generator.KeyPair();
            var (publicKey2b, _) = Generator.KeyPair();
            var (publicKey3b, _) = Generator.KeyPair();
            var endorsements1 = new Endorsement(1, publicKey1a, publicKey1b);
            var endorsements2 = new Endorsement(1, publicKey2a, publicKey2b);
            var endorsements3 = new Endorsement(publicKey3a, publicKey3b);
            var tree = new Endorsement(endorsements1, endorsements2, endorsements3);

            Assert.Equal(KeyType.List, tree.Type);
            Assert.Equal(3U, tree.RequiredCount);
            Assert.Equal(3, tree.List.Length);
            Assert.Empty(tree.PublicKey.ToArray());

            Assert.Equal(KeyType.List, tree.List[0].Type);
            Assert.Equal(1U, tree.List[0].RequiredCount);
            Assert.Equal(2, tree.List[0].List.Length);
            Assert.Empty(tree.List[0].PublicKey.ToArray());

            Assert.Equal(KeyType.Ed25519, tree.List[0].List[0].Type);
            Assert.Equal(0U, tree.List[0].List[0].RequiredCount);
            Assert.Empty(tree.List[0].List[0].List);
            Assert.Equal(publicKey1a.ToArray(), tree.List[0].List[0].PublicKey.ToArray());

            Assert.Equal(KeyType.Ed25519, tree.List[0].List[1].Type);
            Assert.Equal(0U, tree.List[0].List[1].RequiredCount);
            Assert.Empty(tree.List[0].List[1].List);
            Assert.Equal(publicKey1b.ToArray(), tree.List[0].List[1].PublicKey.ToArray());

            Assert.Equal(KeyType.List, tree.List[1].Type);
            Assert.Equal(1U, tree.List[1].RequiredCount);
            Assert.Equal(2, tree.List[1].List.Length);
            Assert.Empty(tree.List[1].PublicKey.ToArray());

            Assert.Equal(KeyType.Ed25519, tree.List[1].List[0].Type);
            Assert.Equal(0U, tree.List[1].List[0].RequiredCount);
            Assert.Empty(tree.List[1].List[0].List);
            Assert.Equal(publicKey2a.ToArray(), tree.List[1].List[0].PublicKey.ToArray());

            Assert.Equal(KeyType.Ed25519, tree.List[1].List[1].Type);
            Assert.Equal(0U, tree.List[1].List[1].RequiredCount);
            Assert.Empty(tree.List[1].List[1].List);
            Assert.Equal(publicKey2b.ToArray(), tree.List[1].List[1].PublicKey.ToArray());

            Assert.Equal(KeyType.List, tree.List[2].Type);
            Assert.Equal(2U, tree.List[2].RequiredCount);
            Assert.Equal(2, tree.List[2].List.Length);
            Assert.Empty(tree.List[2].PublicKey.ToArray());

            Assert.Equal(KeyType.Ed25519, tree.List[2].List[0].Type);
            Assert.Equal(0U, tree.List[2].List[0].RequiredCount);
            Assert.Empty(tree.List[2].List[0].List);
            Assert.Equal(publicKey3a.ToArray(), tree.List[2].List[0].PublicKey.ToArray());

            Assert.Equal(KeyType.Ed25519, tree.List[2].List[1].Type);
            Assert.Equal(0U, tree.List[2].List[1].RequiredCount);
            Assert.Empty(tree.List[2].List[1].List);
            Assert.Equal(publicKey3b.ToArray(), tree.List[2].List[1].PublicKey.ToArray());
        }
        [Fact(DisplayName = "Endorsements: Make List Type from Key type constructor throws error.")]
        public void CreateListTypeFromKeyTypeConstructorThrowsError()
        {
            var (publicKey, _) = Generator.KeyPair();
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new Endorsement(KeyType.List, publicKey);
            });
            Assert.Equal("type", exception.ParamName);
            Assert.StartsWith("Only endorsements representing a single key are supported with this constructor, please use the list constructor instead.", exception.Message);
        }
    }
}
