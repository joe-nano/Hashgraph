﻿using Hashgraph.Test.Fixtures;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Hashgraph.Test.Contract
{
    [Collection(nameof(NetworkCredentials))]
    public class UpdateContractTests
    {
        private readonly NetworkCredentials _network;
        public UpdateContractTests(NetworkCredentials network, ITestOutputHelper output)
        {
            _network = network;
            _network.Output = output;
        }
        [Fact(DisplayName = "Contract Update: Can Update Multiple Properties of Contract (excluding auto-renewal period).")]
        public async Task CanUpdateMultiplePropertiesInOneCall()
        {
            await using var fx = await GreetingContract.CreateAsync(_network);
            var (newPublicKey, newPrivateKey) = Generator.KeyPair();
            var newExpiration = Generator.TruncatedFutureDate(2400, 4800);
            var newEndorsement = new Endorsement(newPublicKey);
            var updatedSignatory = new Signatory(_network.Signatory, newPrivateKey);
            var newMemo = Generator.Code(50);
            fx.Client.Configure(ctx => ctx.Signatory = updatedSignatory);
            var record = await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
            {
                Contract = fx.ContractRecord.Contract,
                Expiration = newExpiration,
                Administrator = newEndorsement,
                Memo = newMemo
            });
            var info = await fx.Client.GetContractInfoAsync(fx.ContractRecord.Contract);
            Assert.NotNull(info);
            Assert.Equal(fx.ContractRecord.Contract, info.Contract);
            Assert.Equal(fx.ContractRecord.Contract, info.Address);
            Assert.Equal(newExpiration, info.Expiration);
            Assert.Equal(newEndorsement, info.Administrator);
            Assert.Equal(fx.ContractParams.RenewPeriod, info.RenewPeriod);
            Assert.Equal(newMemo, info.Memo);
            Assert.Equal((ulong)fx.ContractParams.InitialBalance, info.Balance);
        }
        [Fact(DisplayName = "Contract Update: Can't update properties when Renewal Period is not 7890000 seconds.")]
        public async Task CanUpdateMultiplePropertiesInOneCallButNotRenewalPeriod()
        {
            await using var fx = await GreetingContract.CreateAsync(_network);
            var originalInfo = await fx.Client.GetContractInfoAsync(fx.ContractRecord.Contract);
            var (newPublicKey, newPrivateKey) = Generator.KeyPair();
            var newExpiration = Generator.TruncatedFutureDate(2400, 4800);
            var newEndorsement = new Endorsement(newPublicKey);
            var updatedSignatory = new Signatory(_network.Signatory, newPrivateKey);
            var newRenewPeriod = TimeSpan.FromDays(Generator.Integer(180, 365));
            var newMemo = Generator.Code(50);
            fx.Client.Configure(ctx => ctx.Signatory = updatedSignatory);
            var pex = await Assert.ThrowsAsync<PrecheckException>(async () =>
            {
                var record = await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
                {
                    Contract = fx.ContractRecord.Contract,
                    Expiration = newExpiration,
                    Administrator = newEndorsement,
                    RenewPeriod = newRenewPeriod,
                    Memo = newMemo
                });
            });
            Assert.Equal(ResponseCode.AutorenewDurationNotInRange, pex.Status);
            Assert.StartsWith("Transaction Failed Pre-Check: AutorenewDurationNotInRange", pex.Message);

            var info = await fx.Client.GetContractInfoAsync(fx.ContractRecord.Contract);
            Assert.NotNull(info);
            Assert.Equal(fx.ContractRecord.Contract, info.Contract);
            Assert.Equal(fx.ContractRecord.Contract, info.Address);
            Assert.Equal(originalInfo.Expiration, info.Expiration);
            Assert.Equal(originalInfo.Administrator, info.Administrator);
            Assert.Equal(originalInfo.RenewPeriod, info.RenewPeriod);
            Assert.Equal(originalInfo.Memo, info.Memo);
            Assert.Equal((ulong)fx.ContractParams.InitialBalance, info.Balance);
        }

        [Fact(DisplayName = "Contract Update: Can Update Expiration Date")]
        public async Task UpdateContractExpirationDate()
        {
            await using var fx = await GreetingContract.CreateAsync(_network);

            var oldExpiration = (await fx.Client.GetContractInfoAsync(fx.ContractRecord.Contract)).Expiration;
            var newExpiration = oldExpiration.AddMonths(12);
            var record = await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
            {
                Contract = fx.ContractRecord.Contract,
                Expiration = newExpiration
            });
            var info = await fx.Client.GetContractInfoAsync(fx.ContractRecord.Contract);
            Assert.NotNull(info);
            Assert.Equal(fx.ContractRecord.Contract, info.Contract);
            Assert.Equal(fx.ContractRecord.Contract, info.Address);
            Assert.Equal(newExpiration, info.Expiration);
            Assert.Equal(_network.PublicKey, info.Administrator);
            Assert.Equal(fx.ContractParams.RenewPeriod, info.RenewPeriod);
            Assert.Equal(fx.Memo, info.Memo);
            Assert.Equal((ulong)fx.ContractParams.InitialBalance, info.Balance);
        }

        [Fact(DisplayName = "Contract Update: Can Update Admin Key.")]
        public async Task CanUpdateAdminKey()
        {
            await using var fx = await GreetingContract.CreateAsync(_network);
            var (newPublicKey, newPrivateKey) = Generator.KeyPair();
            var newEndorsement = new Endorsement(newPublicKey);
            var updatedSignatory = new Signatory(_network.Signatory, newPrivateKey);
            fx.Client.Configure(ctx => ctx.Signatory = updatedSignatory);
            var record = await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
            {
                Contract = fx.ContractRecord.Contract,
                Administrator = newEndorsement,
            });
            var info = await fx.Client.GetContractInfoAsync(fx.ContractRecord.Contract);
            Assert.NotNull(info);
            Assert.Equal(fx.ContractRecord.Contract, info.Contract);
            Assert.Equal(fx.ContractRecord.Contract, info.Address);
            Assert.Equal(newEndorsement, info.Administrator);
            Assert.Equal(fx.ContractParams.RenewPeriod, info.RenewPeriod);
            Assert.Equal(fx.Memo, info.Memo);
            Assert.Equal((ulong)fx.ContractParams.InitialBalance, info.Balance);
        }

        [Fact(DisplayName = "Contract Update: Can't Update Renew Period other than 7890000 seconds.")]
        public async Task CanUpdateRenewPeriod()
        {
            await using var fx = await GreetingContract.CreateAsync(_network);
            var newRenewal = TimeSpan.FromDays(Generator.Integer(180, 365));
            var pex = await Assert.ThrowsAsync<PrecheckException>(async () =>
            {
                var record = await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
                {
                    Contract = fx.ContractRecord.Contract,
                    RenewPeriod = newRenewal,
                });
            });
            Assert.Equal(ResponseCode.AutorenewDurationNotInRange, pex.Status);
            Assert.StartsWith("Transaction Failed Pre-Check: AutorenewDurationNotInRange", pex.Message);

            var info = await fx.Client.GetContractInfoAsync(fx.ContractRecord.Contract);
            Assert.NotNull(info);
            Assert.Equal(fx.ContractRecord.Contract, info.Contract);
            Assert.Equal(fx.ContractRecord.Contract, info.Address);
            Assert.Equal(fx.ContractParams.Administrator, info.Administrator);
            Assert.Equal(fx.ContractParams.RenewPeriod, info.RenewPeriod);
            Assert.Equal(fx.Memo, info.Memo);
            Assert.Equal((ulong)fx.ContractParams.InitialBalance, info.Balance);
        }
        [Fact(DisplayName = "Contract Update: Updating Contract Bytecode Silently Fails (IS THIS A NETWORK BUG?)")]
        public async Task CanUpdateContractBytecode()
        {
            await using var fx = await GreetingContract.CreateAsync(_network);
            await using var fx2 = await StatefulContract.SetupAsync(_network);
            var record = await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
            {
                Contract = fx.ContractRecord.Contract,
                File = fx2.FileRecord.File
            });
            var info = await fx.Client.GetContractInfoAsync(fx.ContractRecord.Contract);
            Assert.NotNull(info);
            Assert.Equal(fx.ContractRecord.Contract, info.Contract);
            Assert.Equal(fx.ContractRecord.Contract, info.Address);
            Assert.Equal(fx.ContractParams.Administrator, info.Administrator);
            Assert.Equal(fx.ContractParams.RenewPeriod, info.RenewPeriod);
            Assert.Equal(fx.Memo, info.Memo);
            Assert.Equal((ulong)fx.ContractParams.InitialBalance, info.Balance);

            // Call a method that was not part of the original contract to check update.
            // Note, this is a bug with the network, the call returned success but we are still 
            // able to call the old contract method which should no longer exist.
            var callRecord = await fx.Client.CallContractWithRecordAsync(new CallContractParams
            {
                Contract = fx.ContractRecord.Contract,
                Gas = await _network.TinybarsFromGas(400),
                FunctionName = "greet",
            });
            Assert.NotNull(callRecord);
            Assert.Equal(ResponseCode.Success, record.Status);
            Assert.Empty(callRecord.CallResult.Error);
            Assert.Equal("Hello, world!", callRecord.CallResult.Result.As<string>());
        }
        [Fact(DisplayName = "Contract Update: Can Update Memo.")]
        public async Task CanUpdateMemo()
        {
            await using var fx = await GreetingContract.CreateAsync(_network);
            var newMemo = Generator.Code(50);
            var record = await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
            {
                Contract = fx.ContractRecord.Contract,
                Memo = newMemo
            });
            var info = await fx.Client.GetContractInfoAsync(fx.ContractRecord.Contract);
            Assert.NotNull(info);
            Assert.Equal(fx.ContractRecord.Contract, info.Contract);
            Assert.Equal(fx.ContractRecord.Contract, info.Address);
            Assert.Equal(fx.ContractParams.Administrator, info.Administrator);
            Assert.Equal(fx.ContractParams.RenewPeriod, info.RenewPeriod);
            Assert.Equal(newMemo, info.Memo);
            Assert.Equal((ulong)fx.ContractParams.InitialBalance, info.Balance);
        }
        [Fact(DisplayName = "Contract Update: Can Update Memo, skip Record")]
        public async Task CanUpdateMemoNoRecord()
        {
            await using var fx = await GreetingContract.CreateAsync(_network);
            var newMemo = Generator.Code(50);
            var receipt = await fx.Client.UpdateContractAsync(new UpdateContractParams
            {
                Contract = fx.ContractRecord.Contract,
                Memo = newMemo
            });
            var info = await fx.Client.GetContractInfoAsync(fx.ContractRecord.Contract);
            Assert.NotNull(info);
            Assert.Equal(fx.ContractRecord.Contract, info.Contract);
            Assert.Equal(fx.ContractRecord.Contract, info.Address);
            Assert.Equal(fx.ContractParams.Administrator, info.Administrator);
            Assert.Equal(fx.ContractParams.RenewPeriod, info.RenewPeriod);
            Assert.Equal(newMemo, info.Memo);
            Assert.Equal((ulong)fx.ContractParams.InitialBalance, info.Balance);
        }
        [Fact(DisplayName = "Contract Update: Updating an immutable contract raises error.")]
        public async Task UpdatingImmutableContractRaisesError()
        {
            await using var fx = await GreetingContract.SetupAsync(_network);
            fx.ContractParams.Administrator = null;
            await fx.CompleteCreateAsync();
            var tex = await Assert.ThrowsAsync<TransactionException>(async () =>
            {
                await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
                {
                    Contract = fx.ContractRecord.Contract,
                    Memo = Generator.Code(50)
                });
            });
            Assert.Equal(ResponseCode.ModifyingImmutableContract, tex.Status);
            Assert.StartsWith("Unable to update Contract, status: ModifyingImmutableContract", tex.Message);
        }
        [Fact(DisplayName = "Contract Update: Updating an contract without admin key raises error.")]
        public async Task UpdatingContractWithoutAdminKeyRaisesError()
        {
            var (publicKey, privateKey) = Generator.KeyPair();
            await using var fx = await GreetingContract.SetupAsync(_network);
            fx.ContractParams.Administrator = publicKey;
            fx.Client.Configure(ctx => ctx.Signatory = new Signatory(ctx.Signatory, privateKey));
            await fx.CompleteCreateAsync();
            // First, Remove admin key from Payer's Account
            var tex = await Assert.ThrowsAsync<TransactionException>(async () =>
            {
                fx.Client.Configure(ctx => ctx.Signatory = new Signatory(_network.PrivateKey));
                await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
                {
                    Contract = fx.ContractRecord.Contract,
                    Memo = Generator.Code(50)
                });
            });
            Assert.Equal(ResponseCode.InvalidSignature, tex.Status);
            Assert.StartsWith("Unable to update Contract, status: InvalidSignature", tex.Message);

            // Try again with the admin key to prove we have the keys right
            var record = await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
            {
                Signatory = new Signatory(_network.PrivateKey, privateKey),
                Contract = fx.ContractRecord.Contract,
                Memo = Generator.Code(50)
            });
            Assert.Equal(ResponseCode.Success, record.Status);
        }
        [Fact(DisplayName = "Contract Update: Updating with empty contract address raises error.")]
        public async Task UpdateWithMissingContractRaisesError()
        {
            await using var fx = await GreetingContract.CreateAsync(_network);
            var newMemo = Generator.Code(50);
            var ane = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
                {
                    Memo = newMemo
                });
            });
            Assert.Equal("Contract", ane.ParamName);
            Assert.StartsWith("Contract address is missing. Please check that it is not null.", ane.Message);
        }
        [Fact(DisplayName = "Contract Update: Updating with no changes raises error.")]
        public async Task UpdateWithNoChangesRaisesError()
        {
            await using var fx = await GreetingContract.CreateAsync(_network);
            var ae = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
                {
                    Contract = fx.ContractRecord.Contract
                });
            });
            Assert.Equal("updateParameters", ae.ParamName);
            Assert.StartsWith("The Contract Updates contains no update properties, it is blank.", ae.Message);
        }
        [Fact(DisplayName = "Contract Update: Updating non-existant contract raises error.")]
        public async Task UpdateWithNonExistantContractRaisesError()
        {
            await using var fx = await TestFile.CreateAsync(_network);
            var invalidContractAddress = fx.Record.File;
            await fx.Client.DeleteFileAsync(invalidContractAddress, fx.Signatory);

            var newMemo = Generator.Code(50);
            var pex = await Assert.ThrowsAsync<PrecheckException>(async () =>
            {
                await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
                {
                    Contract = invalidContractAddress,
                    Memo = newMemo
                });
            });
            Assert.Equal(ResponseCode.InvalidContractId, pex.Status);
            Assert.StartsWith("Transaction Failed Pre-Check: InvalidContractId", pex.Message);
        }
        [Fact(DisplayName = "Contract Update: Updating invalid duration raises error.")]
        public async Task UpdateWithInvalidDurationRaisesError()
        {
            await using var fx = await GreetingContract.CreateAsync(_network);
            var newMemo = Generator.Code(50);
            var tex = await Assert.ThrowsAsync<PrecheckException>(async () =>
            {
                await fx.Client.UpdateContractWithRecordAsync(new UpdateContractParams
                {
                    Contract = fx.ContractRecord.Contract,
                    RenewPeriod = TimeSpan.FromDays(Generator.Integer(-90, -60))
                }); ;
            });
            Assert.Equal(ResponseCode.AutorenewDurationNotInRange, tex.Status);
            Assert.StartsWith("Transaction Failed Pre-Check: AutorenewDurationNotInRange", tex.Message);
        }
    }
}
