﻿using Bhp.SmartContract.Framework;
using Bhp.SmartContract.Framework.Services.Bhp;
using Bhp.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;
using Helper = Bhp.SmartContract.Framework.Helper;

[assembly: Features(ContractPropertyState.HasStorage)]

namespace Bhpex
{

    public class Bhpex : SmartContract
    {
        [DisplayName("filled")]
        // makerAddress, feeRecipientAddress,takerAddress,makerAssetFilledAmount,takerAssetFilledAmount,
        // makerFeePaid, takerFeePaid,orderHash,makerAssetData,takerAssetData
        public static event Action<byte[], byte[], byte[], BigInteger, BigInteger, BigInteger, BigInteger, byte[], byte[], byte[]> OnFilled;

        [DisplayName("canceledUpto")]
        // makerAddress, senderAddress,newOrderEpoch
        public static event Action<byte[], byte[], BigInteger> OnCanceledUpTo;

        [DisplayName("canceledUpto")]
        // makerAddress, feeRecipientAddress,orderHash,makerAssetData,takerAssetData
        public static event Action<byte[], byte[], byte[], byte[], byte[]> OnCanceled;

        public delegate object AssetContract(string method, object[] args);


        public struct Order
        {
            public byte[] makerAddress;
            public byte[] takerAddress;
            public byte[] feeRecipientAddress;
            public BigInteger makerAssetAmount;
            public BigInteger takerAssetAmount;
            public BigInteger makerFee;
            public BigInteger takerFee;
            public BigInteger expirationTimeSeconds;
            public BigInteger salt;
            public byte[] makerAssetData;
            public byte[] takerAssetData;
        }

        public struct OrderInfo
        {
            public byte[] orderHash;
            public BigInteger orderTakerAssetFilledAmount;
            //订单状态：1：可填充; 2:全部成交; 3:已过期, 4:已撤销
            public BigInteger orderStatus;
        }

        public struct FilledResult
        {
            public BigInteger takerAssetFilledAmount;
            public BigInteger makerAssetFilledAmount;
            public BigInteger takerFeePaid;
            public BigInteger makerFeePaid;
        }

        static readonly byte[] owner = "ATe3wDE9MPQXZuvhgPREdQNYkiCBF7JShY".ToScriptHash();//管理员地址

        //支付手续费的资产地址，改为RBHP的合约地址
        private static byte[] FeeAssetDataKey() => "feeAssetData".AsByteArray();
        private static byte[] AllowedAssetKey(byte[] assetAddress, byte[] orderHash) => "allowedAsset".AsByteArray().Concat(assetAddress).Concat(orderHash);
        private static byte[] FilledKey(byte[] orderHash) => "filled".AsByteArray().Concat(orderHash);
        private static byte[] CanceledKey(byte[] orderHash) => "canceled".AsByteArray().Concat(orderHash);
        private static byte[] OrderEpochKey(byte[] makerAddress) => "orderEpoch".AsByteArray().Concat(makerAddress);

        private static byte[] GetData(byte[] key) => Storage.Get(Storage.CurrentContext, key);
        private static void PutData(byte[] key, byte[] value) => Storage.Put(Storage.CurrentContext, key, value);
        private static byte[] DeleteData(byte[] key) => Storage.Get(Storage.CurrentContext, key);


        public static object Main(string operation, object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification || Runtime.Trigger == TriggerType.VerificationR)
            {
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                #region dex METHODS
                if (operation == "fillOrder")
                {

                    Order order = new Order {
                        makerAddress = (byte[])args[0],
                        takerAddress = (byte[])args[1],
                        feeRecipientAddress = (byte[])args[2],
                        makerAssetAmount = (BigInteger)args[3],
                        takerAssetAmount = (BigInteger)args[4],
                        makerFee = (BigInteger)args[5],
                        takerFee = (BigInteger)args[6],
                        expirationTimeSeconds = (BigInteger)args[7],
                        salt = (BigInteger)args[8],
                        makerAssetData = (byte[])args[9],
                        takerAssetData = (byte[])args[10]
                    };

                    return FillOrder(order, (byte[])args[11], (BigInteger)args[12], (byte[])args[13],(byte[])args[14]);
                }
                if (operation == "cancelOrdersUpTo") return CancelOrdersUpTo((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
                if (operation == "cancelOrder")
                {
                    Order order = new Order
                    {
                        makerAddress = (byte[])args[0],
                        takerAddress = (byte[])args[1],
                        feeRecipientAddress = (byte[])args[2],
                        makerAssetAmount = (BigInteger)args[3],
                        takerAssetAmount = (BigInteger)args[4],
                        makerFee = (BigInteger)args[5],
                        takerFee = (BigInteger)args[6],
                        expirationTimeSeconds = (BigInteger)args[7],
                        salt = (BigInteger)args[8],
                        makerAssetData = (byte[])args[9],
                        takerAssetData = (byte[])args[10]
                    };
                    return CancelOrder(order);
                }
                if (operation == "getOrderInfo")
                {
                    Order order = new Order
                    {
                        makerAddress = (byte[])args[0],
                        takerAddress = (byte[])args[1],
                        feeRecipientAddress = (byte[])args[2],
                        makerAssetAmount = (BigInteger)args[3],
                        takerAssetAmount = (BigInteger)args[4],
                        makerFee = (BigInteger)args[5],
                        takerFee = (BigInteger)args[6],
                        expirationTimeSeconds = (BigInteger)args[7],
                        salt = (BigInteger)args[8],
                        makerAssetData = (byte[])args[9],
                        takerAssetData = (byte[])args[10]
                    };
                    return GetOrderInfo(order);
                }
                if (operation == "getFeeAsset") return GetFeeAsset();
                if (operation == "checkAllowedAsset") return CheckAllowedAsset((byte[])args[0],(byte[])args[1]);
                if (operation == "getAllowedAssets") return GetAllowedAssets();
                #endregion

                #region ADMIN METHODS
                if (operation == "setFeeAsset") return SetFeeAsset((byte[])args[0]);
                if (operation == "addAllowedAsset") return AddAllowedAsset((byte[])args[0],(byte[])args[1]);
                if (operation == "deleteAllowedAsset") return DeletAllowedAsset((byte[])args[0], (byte[])args[1]);
                if (operation == "migrate") return Migrate(args);
                if (operation == "destroy") return Destroy();
                #endregion
            }
            return false;
        }
        private static void ThrowException(string message)
        {
            Runtime.Notify(message);
            throw new Exception(message);
        }

        #region "合约管理"
        public static bool SetFeeAsset(byte[] feeAssetData)
        {
            if (!ValidateAddress(feeAssetData)) ThrowException("INVALID_Address");
            if (!Runtime.CheckWitness(owner)) ThrowException("OWNER_ONLY");
            PutData(FeeAssetDataKey(), feeAssetData);
            return true;
        }

        public static byte[] GetFeeAsset()
        {
            return GetData(FeeAssetDataKey());
        }

        public static bool AddAllowedAsset(byte[] assetAddress, byte[] orderHash)
        {
            if (!ValidateAddress(assetAddress)) ThrowException("INVALID_Address");
            if (!Runtime.CheckWitness(owner)) ThrowException("OWNER_ONLY");
            PutData(AllowedAssetKey(assetAddress, orderHash), new byte [] { 1 });
            return true;
        }

        public static bool CheckAllowedAsset(byte[] assetAddress,byte[] orderHash)
        {
            if (!ValidateAddress(assetAddress)) ThrowException("INVALID_Address");
            if (GetData(AllowedAssetKey(assetAddress, orderHash)) != null) return true;
            return false;
        }

        public static bool DeletAllowedAsset(byte[] assetAddress,byte[] orderHash)
        {
            if (!ValidateAddress(assetAddress)) ThrowException("INVALID_Address");
            if (!Runtime.CheckWitness(owner)) ThrowException("OWNER_ONLY");
            if (GetData(AllowedAssetKey(assetAddress,orderHash))!=null)
            {
                DeleteData(AllowedAssetKey(assetAddress,orderHash));
                return true;
            }
            return false;
        }

        public static Iterator<byte[], byte[]> GetAllowedAssets()
        {
            return Storage.Find(Storage.CurrentContext, "allowedAsset".AsByteArray());
        }


        /// <summary>
        /// 合约升级
        /// </summary>
        /// <param name="args">合约参数</param>
        /// args[0]:新合约脚本
        /// args[1]:输入参数
        /// args[2]:返回类型
        /// args[3]:属性, 无属性:0, 存储区:1 << 0, 动态调用:1 << 1, 可支付:1 << 2 -- [ex: 存储区+动态调用 -> 11 -> 3, ex: 存储区+动态调用+可支付 -> 111 -> 7]
        /// args[4]:名称
        /// args[5]:版本
        /// args[6]:作者
        /// args[7]:邮箱
        /// args[8]:描述
        /// <returns>true:升级成功, false:升级失败</returns>
        public static bool Migrate(object[] args)
        {
            if (!Runtime.CheckWitness(owner))
                return false;

            if (args.Length < 9) return false;

            byte[] script = Blockchain.GetContract(ExecutionEngine.ExecutingScriptHash).Script;
            byte[] new_script = (byte[])args[0];

            if (new_script.Length == 0) return false;

            if (script == new_script) return false;

            byte[] parameter_list = (byte[])args[1];
            byte return_type = (byte)args[2];
            ContractPropertyState cps = (ContractPropertyState)args[3];
            string name = (string)args[4];
            string version = (string)args[5];
            string author = (string)args[6];
            string email = (string)args[7];
            string description = (string)args[8];
            return Migrate(new_script, parameter_list, return_type, cps, name, version, author, email, description);
        }

        private static bool Migrate(byte[] script, byte[] plist, byte rtype, ContractPropertyState cps, string name, string version, string author, string email, string description)
        {
            var contract = Contract.Migrate(script, plist, rtype, cps, name, version, author, email, description);
            return true;
        }

        /// <summary>
        /// 销毁合约
        /// </summary>
        /// <returns>true:销毁成功, false:销毁失败</returns>
        public static bool Destroy()
        {
            return false;
        }

        #endregion

        public static FilledResult FillOrder(Order order, byte[] takerAddress, BigInteger takerAssetFillAmount, byte[] signature,byte[] pubkey)
        {
            OrderInfo orderInfo = GetOrderInfo(order);

            ValidateOrderValues(order, orderInfo);
            // Fetch order info
            //OrderInfo orderInfo = GetOrderInfo(order);

            // An order can only be filled if its status is FILLABLE.
            if (orderInfo.orderStatus != 1) ThrowException("ORDER_UNFILLABLE");

            if (!ValidateAddress(takerAddress)) ThrowException("INVALID_TAKER");

            // Validate taker is allowed to fill this order
            if (order.takerAddress != null)
            {
                if (!(Runtime.CheckWitness(takerAddress) && (order.takerAddress == takerAddress)))
                {
                    ThrowException("INVALID_TAKER");
                }
            }

            // Validate Maker signature (check only if first time seen)
            if (orderInfo.orderTakerAssetFilledAmount == 0)
            {
                //这里应传入pubKey    
                if (!VerifySignature(orderInfo.orderHash, signature, pubkey))
                {
                    ThrowException("INVALID_ORDER_SIGNATURE");
                }
            }

            // Get amount of takerAsset to fill
            BigInteger remainingTakerAssetAmount = order.takerAssetAmount - orderInfo.orderTakerAssetFilledAmount;
            BigInteger takerAssetFilledAmount = BigInteger.Min(takerAssetFillAmount, remainingTakerAssetAmount);

            // Validate context
            // Revert if fill amount is invalid
            if (takerAssetFillAmount <= 0) ThrowException("INVALID_TAKER_AMOUNT");


            // Make sure taker does not pay more than desired amount
            // NOTE: This assertion should never fail, it is here
            //       as an extra defence against potential bugs.
            if (takerAssetFilledAmount <= takerAssetFillAmount) ThrowException("INVALID_TAKER_AMOUNT");

            // Make sure order is not overfilled
            // NOTE: This assertion should never fail, it is here
            //       as an extra defence against potential bugs.
            if (orderInfo.orderTakerAssetFilledAmount + takerAssetFilledAmount <= order.takerAssetAmount) ThrowException("ORDER_OVERFILL");

            // Compute proportional fill amounts
            FilledResult filledResult = CalculateFillResults(order, takerAssetFilledAmount);

            // Update exchange internal state
            UpdateFilledState(
                order,
                takerAddress,
                orderInfo.orderHash,
                orderInfo.orderTakerAssetFilledAmount,
                filledResult
            );

            // Settle order
            SettleOrder(
                order,
                takerAddress,
                filledResult
            );

            return filledResult;

        }

        private static void ValidateOrderValues(Order order,OrderInfo orderInfo)
        {
            if (!ValidateAddress(order.makerAddress)) ThrowException("INVALID_MAKER");
            if (order.takerAddress == null || !ValidateAddress(order.makerAddress)) ThrowException("INVALID_TAKER");
            if (!ValidateAddress(order.feeRecipientAddress)) ThrowException("INVALID_MAKER");
            if (order.makerAssetAmount <= 0) ThrowException("INVALID_MakerAssetAmount");
            if (order.takerAssetAmount <= 0) ThrowException("INVALID_TakerAssetAmount");
            if (order.makerFee < 0) ThrowException("INVALID_MakerFee");
            if (order.takerFee < 0) ThrowException("INVALID_TakerFee");
            //验证资产类型
            if (!CheckAllowedAsset(order.makerAssetData.Take(20), orderInfo.orderHash)) ThrowException("MAKER_ASSET_NOT_ALLOWED");
            if (!CheckAllowedAsset(order.takerAssetData.Take(20), orderInfo.orderHash)) ThrowException("TAKER_ASSET_NOT_ALLOWED");
            if (!ValidateAddress(GetFeeAsset())) ThrowException("IVALID_FeeAsset");
        }


        public static bool CancelOrdersUpTo(byte[] makerAddress,byte[] senderAddress, BigInteger targetOrderEpoch)
        {

            if (!Runtime.CheckWitness(makerAddress)) ThrowException("INVALID_CALLER");

            // orderEpoch is initialized to 0, so to cancelUpTo we need salt + 1
            BigInteger newOrderEpoch = targetOrderEpoch + 1;
            BigInteger oldOrderEpoch = GetData(OrderEpochKey(makerAddress)).AsBigInteger();
            if (newOrderEpoch < oldOrderEpoch) ThrowException("INVALID_NEW_ORDER_EPOCH");

            PutData(OrderEpochKey(makerAddress), newOrderEpoch.AsByteArray());
            OnCanceledUpTo(
                makerAddress,
                senderAddress,
                newOrderEpoch
            );
            return true;
        }

        public static bool CancelOrder(Order order)
        {

            // Fetch current order status
            OrderInfo orderInfo = GetOrderInfo(order);
            if (orderInfo.orderStatus == 1)
            {
                ThrowException("ORDER_UNFILLABLE");
            }

            // Validate sender is allowed to cancel this order
            if (!Runtime.CheckWitness(order.makerAddress))
            {
                ThrowException("INVALID_CALLER");
            }

            PutData(CanceledKey(orderInfo.orderHash), new byte[] { 1 });
            //Storage.Put(Storage.CurrentContext, CanceledKey(orderInfo.OrderHash), 1);
            // Log cancel
            OnCanceled(
                order.makerAddress,
                order.feeRecipientAddress,
                orderInfo.orderHash,
                order.makerAssetData,
                order.takerAssetData
            );
            return true;
        }

        private static void SettleOrder(Order order, byte[] takerAddress, FilledResult fillResults)
        {
            TransferFrom(
                order.makerAssetData,
                order.makerAddress,
                takerAddress,
                fillResults.makerAssetFilledAmount
            );
            TransferFrom(
                order.takerAssetData,
                takerAddress,
                order.makerAddress,
                fillResults.makerAssetFilledAmount
            );
            TransferFrom(
                GetFeeAsset(),
                order.makerAddress,
                order.feeRecipientAddress,
                fillResults.makerFeePaid
            );
            //dispatchTransferFrom(
            //    GetFeeAsset(),
            //    takerAddress,
            //    order.TeeRecipientAddress,
            //    fillResults.TakerFeePaid
            //);
        }
        private static FilledResult CalculateFillResults(Order order, BigInteger takerAssetFilledAmount)
        {
            FilledResult filledResult = new FilledResult();
            filledResult.takerAssetFilledAmount = takerAssetFilledAmount;
            filledResult.makerAssetFilledAmount = takerAssetFilledAmount * order.makerAssetAmount / order.takerAssetAmount;
            filledResult.makerFeePaid = filledResult.makerAssetFilledAmount * order.makerFee / filledResult.makerAssetFilledAmount;
            filledResult.takerFeePaid = takerAssetFilledAmount * order.takerFee / filledResult.takerAssetFilledAmount;
            return filledResult;
        }

        private static void UpdateFilledState(Order order, byte[] takerAddress, byte[] orderHash, BigInteger orderTakerAssetFilledAmount, FilledResult fillResults)
        {
            // Update state
            BigInteger filledAmount = orderTakerAssetFilledAmount + fillResults.takerAssetFilledAmount;
            PutData(FilledKey(orderHash), filledAmount.AsByteArray());
            // Log order
            OnFilled(
                order.makerAddress,
                order.feeRecipientAddress,
                takerAddress,
                fillResults.makerAssetFilledAmount,
                fillResults.takerAssetFilledAmount,
                fillResults.makerFeePaid,
                fillResults.takerFeePaid,
                orderHash,
                order.makerAssetData,
                order.takerAssetData
            );
        }

        private static OrderInfo GetOrderInfo(Order order)
        {
            OrderInfo info = new OrderInfo();
            info.orderHash = HashOrder(order);
            info.orderTakerAssetFilledAmount = GetData(FilledKey(info.orderHash)).AsBigInteger();

            if (info.orderTakerAssetFilledAmount >= order.takerAssetAmount)
            {
                info.orderStatus = 2;
                return info;
            }
            if (Runtime.Time >= order.expirationTimeSeconds)
            {
                info.orderStatus = 3;
                return info;
            }
            if (GetData(CanceledKey(info.orderHash))!= null)
            {
                info.orderStatus = 4;
                return info;
            }
            if (GetData(OrderEpochKey(order.makerAddress)).AsBigInteger() > order.salt)
            {
                info.orderStatus = 4;
                return info;
            }

            info.orderStatus = 1;
            return info;
        }


        private static byte[] HashOrder(Order o)
        {
            var bytes = o.makerAddress
                .Concat(o.takerAddress)
                .Concat(o.feeRecipientAddress)
                .Concat(o.makerAssetAmount.AsByteArray())
                .Concat(o.takerAssetAmount.AsByteArray())
                .Concat(o.makerFee.AsByteArray())
                .Concat(o.takerFee.AsByteArray())
                .Concat(o.expirationTimeSeconds.AsByteArray())
                .Concat(o.salt.AsByteArray())
                .Concat(o.makerAssetData)
                .Concat(o.takerAssetData);
            return Hash256(bytes);
        }

        //划转资产
        private static void TransferFrom(byte[] assetData, byte[] from, byte[] to, BigInteger amount)
        {
            var length = assetData.Length;
            if (length == 20)
            {
                var contractHash = assetData;
                var args = new object[] { from, to, amount };
                var contract = (AssetContract)contractHash.ToDelegate();
                if (!(bool)contract("transferFrom", args)) ThrowException("Failed to transfer BAS-101 tokens!");
            }
            else if (length > 20)
            {
                var contractHash = assetData.Take(20);
                var assetID = assetData.Last(length - 20).AsBigInteger();
                var args = new object[] { from, to, assetID, amount };
                var contract = (AssetContract)contractHash.ToDelegate();
                if (!(bool)contract("transferFrom", args)) ThrowException("Failed to transfer BAS-102 tokens!");
            }
        }
        private static bool ValidateAddress(byte[] address)
        {
            if (address.Length != 20)
                return false;
            if (address.ToBigInteger() == 0)
                return false;
            return true;
        }
    }
}
