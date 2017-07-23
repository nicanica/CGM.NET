﻿using CGM.Communication;
using CGM.Communication.Interfaces;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using CGM.Communication.Extensions;
using CGM.Communication.MiniMed.Responses;
using CGM.Communication.Common.Serialize;
using CGM.Communication.Patterns;
using CGM.Communication.Common;
using CGM.Communication.MiniMed.Requests.Standard;
using CGM.Communication.MiniMed.Responses.Patterns;
using System.Linq;
using CGM.Communication.Data.Repository;

namespace CGM.Communication.MiniMed
{
    public class MiniMedContext : BaseContext
    {


        public MiniMedContext(IDevice device) : base(device)
        {
        }

        public MiniMedContext(IDevice device, SerializerSession session) : base(device)
        {
            this.Session = session;
        }


        public async Task<BayerStickInfoResponse> GetDeviceInformationAsync(CancellationToken cancelToken)
        {
            try
            {

                await StartCollectDeviceInfoAsync(cancelToken);
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
            }
            finally
            {
                await CloseAsync(cancelToken);
            }


            return Session.Device;
        }


        public async Task<SerializerSession> GetSessionAsync(CancellationToken cancelToken)
        {

            List<Func<Task>> tasks = new List<Func<Task>>();
            tasks.Add(() => StartCollectPumpSettingsAsync(cancelToken));
            return await CallPumpWithActions(tasks, cancelToken);
        }

        public async Task<SerializerSession> GetPumpSessionAsync(CancellationToken cancelToken)
        {
            List<Func<Task>> tasks = new List<Func<Task>>();
            tasks.Add(() => StartPumpTimeAsync(cancelToken));
            tasks.Add(() => StartCollectPumpDataAsync(cancelToken));

            //should be on the settings, some kind of parameters/flags
            if (1 == 2)
            {
                tasks.Add(() => StartBasalPatternAsync(cancelToken));
                tasks.Add(() => StartGetCarbRatio(cancelToken));
                tasks.Add(() => StartReadHistoryInfoAsync(cancelToken));
                tasks.Add(() => StartReadHistoryAsync(cancelToken));
                tasks.Add(() => StartMultiPacketAsync(cancelToken));
            }

            return await CallPumpWithActions(tasks, cancelToken);
        }


        public async Task<SerializerSession> GetPumpConfigurationAsync(CancellationToken cancelToken)
        {
            //right now, just the basal profiles.....
            List<Func<Task>> tasks = new List<Func<Task>>();
            tasks.Add(() => StartBasalPatternAsync(cancelToken));
            return await CallPumpWithActions(tasks, cancelToken);
        }

        private async Task<SerializerSession> CallPumpWithActions(List<Func<Task>> tasks, CancellationToken cancelToken)
        {

            try
            {
                cancelToken.ThrowIfCancellationRequested();
                await StartCollectDeviceInfoAsync(cancelToken);
                Logger.LogInformation($"Call pump with CNL: {this.Session.Device.SerialNumber} ");

                try
                {
                    cancelToken.ThrowIfCancellationRequested();
                    await BeginModesAsync(cancelToken);

                    try
                    {
                        cancelToken.ThrowIfCancellationRequested();
                        await OpenConnectionAsync(cancelToken);

                        try
                        {
                            cancelToken.ThrowIfCancellationRequested();
                            await StartCollectPumpSettingsAsync(cancelToken);

                            try
                            {
                                cancelToken.ThrowIfCancellationRequested();
                                await StartChannelNegoationAsync(cancelToken);

                                try
                                {
                                    cancelToken.ThrowIfCancellationRequested();
                                    await BeginEHSMAsync(cancelToken);

                                    try
                                    {
                                        foreach (var item in tasks)
                                        {
                                            cancelToken.ThrowIfCancellationRequested();
                                            await item();

                                        }
                                    }
                                    catch (Exception e)
                                    {

                                        Logger.LogError(e.Message);
                                    }
                                }
                                catch (Exception e)
                                {

                                    Logger.LogError(e.Message);
                                }
                                finally
                                {
                                    if (Device.IsConnected)
                                    {
                                        await EndEHSMAsync(cancelToken);
                                    }
                                    
                                }


                            }
                            catch (Exception e)
                            {

                                Logger.LogError(e.Message);
                            }

                        }
                        catch (Exception e)
                        {
                            Logger.LogError(e.Message);
                        }

                    }
                    catch (Exception e)
                    {

                        Logger.LogError(e.Message);
                    }
                    finally
                    {
                        if (Device.IsConnected)
                        {
                            await CloseConnectionAsync(cancelToken);
                        }
                    }
                }
                catch (Exception e)
                {

                    Logger.LogError(e.Message);
                }
                finally
                {
                    if (Device.IsConnected)
                    {
                        await EndModesAsync(cancelToken);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
            }
            finally
            {
                if (Device.IsConnected)
                {
                    await CloseAsync(cancelToken);
                }
            }

            return Session;
        }

        private async Task StartCollectDeviceInfoAsync(CancellationToken cancelToken)
        {


                Logger.LogInformation("Getting CNL deviceInformation");


                CommunicationBlock block = new CommunicationBlock();

                block.Request = new AstmStart("X");
                //expected responses for the request
                block.ExpectedResponses.Add(new ReportPattern(new byte[] { 0x04, 0x02 }, 5));
                block.ExpectedResponses.Add(new EnqOREotkPattern());
                //Start Communication 
                await this.StartCommunication(block, cancelToken);
            
            if (string.IsNullOrEmpty(this.Session.Device.SerialNumber))
            {
                throw new Exception("DeviceInfo not set.");
            }
            else
            {
                //Get previous saved parameters Or set this session if device do not exsist
                using (CgmUnitOfWork uow = new CgmUnitOfWork())
                {
                    uow.Device.GetOrSetSessionAndSettings(Session);
                }
            }







        }

        private async Task BeginModesAsync(CancellationToken cancelToken)
        {
            Logger.LogInformation("Enter ControlMode");
            await StartCommunication(new NAKMessage(), new EnqOREotkPattern(), cancelToken);

            await StartCommunication(new ENQMessage(), new EnqORAckkPattern(), cancelToken);


            Logger.LogInformation("Enter PassthroughMode");
            await StartCommunication(new AstmStart("W|"), new ACKPattern(), cancelToken);

            await StartCommunication(new AstmStart("Q|"), new ACKPattern(), cancelToken);

            await StartCommunication(new AstmStart("1|"), new ACKPattern(), cancelToken);


        }

        private async Task OpenConnectionAsync(CancellationToken cancelToken)
        {
            Logger.LogInformation($"Open Connection");

            if (this.Session.Device != null && this.Session.Device.HMACbyte != null)
            {
               //Logger.LogInformation($"OpenConnection HMAC: {BitConverter.ToString(this.Session.Device.HMACbyte)}");
                await StartCommunication(Session.GetOpenConnectionRequest(),
                    new OpenConnectionResponsePattern(),
                    cancelToken);
                Logger.LogInformation($"Connection opened");
            }
            else
            {
                throw new Exception("HMACbyte from CNL is null.");
            }

        }

        private async Task StartCollectPumpSettingsAsync(CancellationToken cancelToken)
        {
            //test this....
            if (this.Session.LinkMac == null && this.Session.PumpMac == null)
            {
                Logger.LogInformation($"Getting linkmac/Pumpmac");
                await StartCommunication(Session.GetReadInfoRequest(),
                        new ReadInfoResponsePattern(),
                        cancelToken);
                Logger.LogInformation($"Got LinkMac: {BitConverter.ToString(this.Session.LinkMac)} AND PumpMac: {BitConverter.ToString(this.Session.PumpMac)}");


                if (this.Session.LinkMac == null && this.Session.PumpMac == null)
                {
                    throw new Exception("Error getting Linkmac/Pumpmac");
                }
                else
                {
                    //save macs
                    using (CgmUnitOfWork uow = new CgmUnitOfWork())
                    {
                        uow.Device.AddUpdateSessionToDevice(Session);
                    }
                }

            }

  

            if (this.Session.LinkKey == null)
            {

                Logger.LogInformation($"Getting linkkey");
                await StartCommunication(Session.GetLinkKeyRequest(),
        new LinkKeyResponsePattern(),
        cancelToken);


                if (this.Session.LinkKey == null)
                {
                    throw new Exception("Error getting linkkey");
                }
                else
                {
                    Logger.LogInformation($"Got LinkKey: {BitConverter.ToString(this.Session.LinkKey)}");
                    //save LinkKey
                    using (CgmUnitOfWork uow = new CgmUnitOfWork())
                    {
                        uow.Device.AddUpdateSessionToDevice(Session);
                    }
                }

            }


            if (this.Session.LinkMac == null || this.Session.PumpMac == null)
            {
                throw new Exception($"Could not get linkmac/pumpmac: {this.Session.LinkMac}/{this.Session.PumpMac}");
            }

        }

        private async Task StartChannelNegoationAsync(CancellationToken cancelToken)
        {
            //IEEE 802.15.4 Channel ID
            //0x0e - Channel 14 - 2420MHz
            //0x11 - Channel 17 - 2435MHz
            //0x14 - Channel 20 - 2450MHz
            //0x17 - Channel 23 - 2465MHz
            //0x1a - Channel 26 - 2480MHz
            List<byte> channels = new List<byte>() { 0x1a, 0x17, 0x14, 0x0e, 0x11 };
            //short list of channels. 
            //channel 23: observed loosing connection between sensor and¨pump when on channel 23. NOT GOOD.
            //channel 26: never seen a connection on this channel. remove to save loop-time.
            //the above channels removed from list to save loop-time and increase stability.
            //List<byte> channels = new List<byte>() {  0x14, 0x0e, 0x11 };
            byte lastChannel = this.Session.RadioChannel;


            if (this.Session.RadioChannelConfirmed && this.Session.RadioChannel == 0x00)
            {
                this.Session.RadioChannelConfirmed = false;
            }

           
            if (this.Session.RadioChannel != 0x00)
            {
                Logger.LogInformation($"Looking for pump. Channel: {this.Session.RadioChannel} (Last used)");
                await StartCommunicationStandardResponse(Session.GetChannelRequest(this.Session.RadioChannel), cancelToken);
            }


            if (this.Session.RadioChannel == 0x00)
            {

                if (lastChannel != 0x00)
                {
                    channels.Remove(lastChannel);
                }

                foreach (var item in channels)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    Logger.LogInformation($"Looking for pump. Channel: {item}");
                    await StartCommunicationStandardResponse(Session.GetChannelRequest(item), cancelToken);
                    
                    if (this.Session.RadioChannel != 0x00)
                    {
                        break;
                    }
                    else
                    {
                        Logger.LogInformation($"No connection on Channel {item}");
                    }

                }
            }

            if (this.Session.RadioSignalStrength==0 && this.Session.RadioChannel != 0x00)
            {
                Logger.LogInformation($"Signal on Radiochannel {this.Session.RadioChannel.ToString()} is too weak ({this.Session.RadioSignalStrength}%)");
                this.Session.RadioChannel = 0x00;
            }

            if (this.Session.RadioChannel == 0x00)
            {
                this.Session.RadioChannelConfirmed = false;
                throw new Exception("Could not find RadioChannel/Pump.");
            }
            else
            {
                this.Session.RadioChannelConfirmed = true;
                Logger.LogInformation($"Connected on radiochannel {this.Session.RadioChannel.ToString()}. ({this.Session.RadioSignalStrength}%)");
                //save LinkKey
                using (CgmUnitOfWork uow = new CgmUnitOfWork())
                {
                    uow.Device.AddUpdateSessionToDevice(Session);
                }
            }


            Logger.LogTrace(this.Session.GetParametersAsString());
        }

        private async Task BeginEHSMAsync(CancellationToken cancelToken)
        {
            Logger.LogInformation("Begin EHSM");
            await StartCommunication(Session.GetBeginEHSM(),
                    new SendMessageResponsePattern(),
                    cancelToken);
        }

        private async Task EndEHSMAsync(CancellationToken cancelToken)
        {

            Logger.LogInformation("End EHSM");
            await StartCommunication(Session.GetEndEHSM(),
                    new SendMessageResponsePattern(),
                    cancelToken);
        }

        private async Task StartBasalPatternAsync(CancellationToken cancelToken)
        {
            //Dictionary<int, List<UnitStartTime>> current = this.Session.BasalPatterns;
            //this.Session.BasalPatterns = new Dictionary<int, List<UnitStartTime>>();

            Logger.LogInformation("Getting BasalPatterns");
            ////check for 8 PumpBasal
            for (int i = 1; i <= 8; i++)
            {
                await StartCommunicationStandardResponse(Session.GetPumpBasalPattern(i), cancelToken);
            }


            //TODO: check if new basalPatterns, maybe save to SQLite and publish to profile on nightscout, if changed (but not here)

        }

        private async Task StartPumpTimeAsync(CancellationToken cancelToken)
        {

            Logger.LogInformation("Getting Pumptime");
            await StartCommunicationStandardResponse(Session.GetPumpTime(), cancelToken);

            if (Session.PumpTime != null && Session.PumpTime.PumpDateTime.HasValue)
            {
                Logger.LogInformation($"Got pumptime: {Session.PumpTime.PumpDateTime.Value.ToString()}");
            }
            else
            {
                throw new Exception("PumpDate has no value.");
            }
        }

        private async Task StartCollectPumpDataAsync(CancellationToken cancelToken)
        {

            Logger.LogInformation("Getting Pumpstatus");
            await StartCommunicationStandardResponse(Session.GetPumpData(), cancelToken);
            if (Session.Status.Count>0)
            {
                Logger.LogInformation($"Got pumpstatus: {Session.Status.Last().ToString()}");
                Logger.LogTrace($"Decrypted bytes: {BitConverter.ToString(Session.Status.Last().AllBytes)}");
            }
        }

        private async Task StartReadHistoryInfoAsync(CancellationToken cancelToken)
        {
            //for now - Read only one day.....
            DateTime lastReadDateTime = DateTime.Now;
            Logger.LogInformation("ReadHistoryInfo");
            await StartCommunicationStandardResponse(Session.GetReadHistoryInfo(lastReadDateTime), cancelToken);
        }

        //

        private async Task StartGetCarbRatio(CancellationToken cancelToken)
        {

            DateTime lastReadDateTime = DateTime.Now;
            Logger.LogInformation("ReadCarbRatio");
            await StartCommunicationStandardResponse(Session.GetCarbRatio(), cancelToken);
        }
        private async Task StartReadHistoryAsync(CancellationToken cancelToken)
        {
            //for now - Read only one day.....
            DateTime lastReadDateTime = DateTime.Now;
            Logger.LogInformation("ReadHistory");

            CommunicationBlock communicationBlock = new CommunicationBlock();
            communicationBlock.Request = Session.GetReadHistory(lastReadDateTime);
            communicationBlock.ExpectedResponses.Add(new SendMessageResponsePattern());
            communicationBlock.ExpectedResponses.Add(new RecieveMessageResponsePattern());
            communicationBlock.ExpectedResponses.Add(new RecieveMessageResponsePattern());
            communicationBlock.ExpectedResponses.Add(new RecieveMessageResponsePattern());


            await StartCommunication(communicationBlock, cancelToken);
        }

        private async Task StartMultiPacketAsync(CancellationToken cancelToken)
        {


            Logger.LogInformation("MultiPacket");

            CommunicationBlock communicationBlock = new CommunicationBlock();
            communicationBlock.Request = Session.GetMultiPacket();
            //get the first 30.... 
            for (int i = 0; i < 30; i++)
            {
                communicationBlock.ExpectedResponses.Add(new RecieveMessageResponsePattern());
            }

            //TODO: need to handle x amount of recieved messages.
            await StartCommunication(communicationBlock, cancelToken);
        }

        private async Task EndModesAsync(CancellationToken cancelToken)
        {

            Logger.LogInformation("End PassthroughMode");
            await StartCommunication(new AstmStart("W|"), new ACKPattern(), cancelToken);
            await StartCommunication(new AstmStart("Q|"), new ACKPattern(), cancelToken);
            await StartCommunication(new AstmStart("0|"), new ACKPattern(), cancelToken);
        }

        private async Task CloseConnectionAsync(CancellationToken cancelToken)
        {
            Logger.LogInformation($"Closing connection");
            await StartCommunication(Session.GetCloseConnectionRequest(), new CloseConnectionResponsePattern(), cancelToken);
        }

        private async Task CloseAsync(CancellationToken cancelToken)
        {
            Logger.LogInformation("Close CNL");
            await StartCommunication(new EOTMessage(), cancelToken);
        }

    }
}