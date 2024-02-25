using Lagrange.Core.Common;
using Lagrange.Core.Internal.Event;
using Lagrange.Core.Internal.Event.Message;
using Lagrange.Core.Internal.Packets.Message.Component;
using Lagrange.Core.Internal.Packets.Service.Oidb;
using Lagrange.Core.Internal.Packets.Service.Oidb.Common;
using Lagrange.Core.Utility.Binary;
using Lagrange.Core.Utility.Extension;
using ProtoBuf;
using FileInfo = Lagrange.Core.Internal.Packets.Service.Oidb.Common.FileInfo;
using GroupInfo = Lagrange.Core.Internal.Packets.Service.Oidb.Common.GroupInfo;

namespace Lagrange.Core.Internal.Service.Message;

[EventSubscribe(typeof(RecordUploadEvent))]
[Service("OidbSvcTrpcTcp.0x126e_100")]
internal class RecordUploadService : BaseService<RecordUploadEvent>
{
    protected override bool Build(RecordUploadEvent input, BotKeystore keystore, BotAppInfo appInfo, BotDeviceInfo device,
        out BinaryPacket output, out List<BinaryPacket>? extraPackets)
    {
        if (input.Entity.AudioStream is null) throw new Exception();
        
        string md5 = input.Entity.AudioStream.Md5(true);
        string sha1 = input.Entity.AudioStream.Sha1(true);
        
        var packet = new OidbSvcTrpcTcpBase<NTV2RichMediaReq>(new NTV2RichMediaReq
        {
            ReqHead = new MultiMediaReqHead
            {
                Common = new CommonHead
                {
                    RequestId = input.GroupUin == null ? 4u : 1u,
                    Command = 100
                },
                Scene = new SceneInfo
                {
                    RequestType = 2,
                    BusinessType = 3,
                    SceneType = 2,
                },
                Client = new ClientMeta { AgentType = 2 },
            },
            Upload = new UploadReq
            {
                UploadInfo = new List<UploadInfo>
                {
                    new()
                    {
                        FileInfo = new FileInfo
                        {
                            FileSize = (uint)input.Entity.AudioStream.Length,
                            FileHash = md5,
                            FileSha1 = sha1,
                            FileName = md5 + ".amr",
                            Type = new FileType
                            {
                                Type = 3,
                                PicFormat = 0,
                                VideoFormat = 0,
                                VoiceFormat = 1
                            },
                            Width = 0,
                            Height = 0,
                            Time = (uint)input.Entity.AudioLength,
                            Original = 0
                        },
                        SubFileType = 0
                    }
                },
                TryFastUploadCompleted = true,
                SrvSendMsg = false,
                ClientRandomId = (ulong)Random.Shared.Next(),
                CompatQMsgSceneType = 2,
                ExtBizInfo = new ExtBizInfo
                {
                    Ptt = new PttExtBizInfo
                    {
                        BytesReserve = Array.Empty<byte>(),
                        BytesPbReserve = new byte[] { 0x08, 0x00, 0x38, 0x00 },
                        BytesGeneralFlags = new byte[] { 0x9a, 0x01, 0x07, 0xaa, 0x03, 0x04, 0x08, 0x08, 0x12, 0x00 }
                    }
                },
                ClientSeq = 0,
                NoNeedCompatMsg = false
            }
        }, 0x126e, 100, false, true);
        
        if (input.GroupUin != null)
        {
            packet.Body.ReqHead.Scene.Group = new GroupInfo { GroupUin = input.GroupUin.Value };
        }
        else
        {
            packet.Body.ReqHead.Scene.C2C = new C2CUserInfo
            {
                AccountType = 2,
                SelfUid = keystore.Uid ?? ""
            };
        }
        
        output = packet.Serialize();
        extraPackets = null;
        return true;
    }

    protected override bool Parse(byte[] input, BotKeystore keystore, BotAppInfo appInfo, BotDeviceInfo device,
        out RecordUploadEvent output, out List<ProtocolEvent>? extraEvents)
    {
        var packet = Serializer.Deserialize<OidbSvcTrpcTcpResponse<NTV2RichMediaResp>>(input.AsSpan());
        var upload = packet.Body.Upload;
        var compat = Serializer.Deserialize<RichText>(upload.CompatQMsg.AsSpan());
        
        output = RecordUploadEvent.Result((int)packet.ErrorCode, upload.UKey, upload.MsgInfo, upload.IPv4s, compat);
        extraEvents = null;
        return true;
    }
}