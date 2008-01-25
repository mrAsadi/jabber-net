using System.Xml;
using jabber;
using jabber.connection;
using jabber.protocol;
using jabber.protocol.client;
using jabber.protocol.iq;
using NUnit.Framework;
using Rhino.Mocks;
using Rhino.Mocks.Interfaces;

namespace test.jabber.connection
{
    [TestFixture]
    public class ConferenceManagerTest
    {
        private MockRepository mocks;
        private ConferenceManager cm;
        private XmppStream stream;
        private IIQTracker tracker;

        private XmlDocument doc;

        private readonly JID jid = new JID("room", "conference.test.com", "nick");

        [SetUp]
        public void setup()
        {
            mocks = new MockRepository();
            cm = new ConferenceManager();
            stream = mocks.DynamicMock<XmppStream>();
            tracker = mocks.DynamicMock<IIQTracker>();
            cm.Stream = stream;

            doc = new XmlDocument();
        }

        [Test]
        public void GetRoomTest()
        {
            Room test = cm.GetRoom(jid);
            Assert.IsNotNull(test);
        }

        [Test]
        public void HasRoomTest()
        {
            bool roomExists = cm.HasRoom(jid);
            Assert.IsFalse(roomExists);

            cm.GetRoom(jid);
            roomExists = cm.HasRoom(jid);
            Assert.IsTrue(roomExists);
        }

        [Test]
        public void RemoveRoomTest()
        {
            cm.GetRoom(jid);
            bool roomExists = cm.HasRoom(jid);
            Assert.IsTrue(roomExists);

            cm.RemoveRoom(jid);
            roomExists = cm.HasRoom(jid);
            Assert.IsFalse(roomExists);
        }

        private delegate T Func<A0, T>(A0 arg0);

        [Test]
        public void RoomJoinTest()
        {
            using (mocks.Record())
            {
                Expect.Call(stream.Document).Return(doc);
                stream.Write((XmlElement)null);
                LastCall.Callback((Func<XmlElement, bool>)
                    delegate(XmlElement elem)
                    {
                        string original = elem.OuterXml;
                        return original.Replace(" ", "") == GetJoinPresence().Replace(" ", "");
                    });
                stream.OnProtocol += null;
                LastCall.IgnoreArguments();
            }

            using (mocks.Playback())
            {
                Room testRoom = cm.GetRoom(jid);
                testRoom.Join();
            }
        }

        [Test]
        public void RoomJoinDefaultConfigTest()
        {
            RoomConfigTest(true);
        }

        [Test]
        public void RoomJoinGetConfigTest()
        {
            RoomConfigTest(false);
        }
        
        private void RoomConfigTest(bool defaultConfig)
        {
            using (mocks.Record())
            {
                Expect.Call(stream.Document).Return(doc);

                stream.OnProtocol += null;
                IEventRaiser onProtocol = LastCall.IgnoreArguments().GetEventRaiser();

                stream.Write((XmlElement)null);
                LastCall.Callback((Func<XmlElement, bool>)
                    delegate(XmlElement elem)
                    {
                        onProtocol.Raise(new object[] { null, CreateJoinResponsePacket(elem) });
                        return true;
                    });

                Expect.Call(stream.Document).Return(doc);
                SetupTrackerBeginIq(delegate(IQ iq, IqCB cb, object cbArg)
                    {
                        string id = iq.GetAttribute("id");
                        string config = defaultConfig ? GetDefaultConfigPacket(id) :
                            GetRetrieveConfigPacket(id);
                        return iq.OuterXml.Replace(" ", "") == config.Replace(" ", "");
                    });
            }

            using (mocks.Playback())
            {
                Room testRoom = cm.GetRoom(jid);
                testRoom.DefaultConfig = defaultConfig;
                testRoom.Join();
            }
        }

        private string GetRetrieveConfigPacket(string id)
        {
            return
                string.Format(
                    "<iq id=\"{0}\" type=\"get\" to=\"{1}\">"+
                        "<query xmlns=\"{2}\"/>"+
                    "</iq>",
                    id, jid.Bare, URI.MUC_OWNER);
        }

        private string GetDefaultConfigPacket(string id)
        {
            return
                string.Format(
                    "<iq id=\"{0}\" type=\"set\" to=\"{1}\">" +
                        "<query xmlns=\"{2}\">" +
                            "<x type=\"submit\" xmlns=\"{3}\"/>" +
                        "</query>" +
                    "</iq>",
                    id, jid.Bare, URI.MUC_OWNER, URI.XDATA);
        }

        private delegate T Func<A0, A1, A2, T>(A0 arg0, A1 arg1, A2 arg2);

        private void SetupTrackerBeginIq(Func<IQ, IqCB, object, bool> func)
        {
            Expect.Call(stream.Tracker).Return(tracker);
            tracker.BeginIQ(null, null, null);
            LastCall.Callback(func);
        }

        private XmlElement CreateJoinResponsePacket(XmlElement elem)
        {
            XmlDocument myDoc = new XmlDocument();

            RoomPresence presence = new RoomPresence(myDoc, jid);
            presence.RemoveAll();
            presence.From = elem.GetAttribute("to");

            UserX xElem = new UserX(myDoc);
            presence.AppendChild(xElem);

            xElem.Status = new RoomStatus[] { RoomStatus.CREATED, RoomStatus.SELF };

            return presence;
        }

        private const string MESSAGE = "TestMessage";

        [Test]
        public void RoomMessageTest()
        {
            using (mocks.Record())
            {
                Expect.Call(stream.Document).Return(doc);
                stream.Write((XmlElement)null);
                LastCall.Callback((Func<XmlElement, bool>)
                    delegate(XmlElement elem)
                    {
                        string id = elem.GetAttribute("id");
                        string original = elem.OuterXml;
                        return original.Replace(" ", "") == GetRoomMessage(id).Replace(" ", "");
                    });
                stream.OnProtocol += null;
                LastCall.IgnoreArguments();
            }

            using (mocks.Playback())
            {
                Room testRoom = cm.GetRoom(jid);
                testRoom.PublicMessage(MESSAGE);
            }
        }

        private const string TO_NICK = "TestNick";

        [Test]
        public void RoomPrivateMessageTest()
        {
            using (mocks.Record())
            {
                Expect.Call(stream.Document).Return(doc);
                stream.Write((XmlElement)null);
                LastCall.Callback((Func<XmlElement, bool>)
                    delegate(XmlElement elem)
                    {
                        string id = elem.GetAttribute("id");
                        string original = elem.OuterXml;
                        return original.Replace(" ", "") ==
                            GetRoomPrivateMessage(id).Replace(" ", "");
                    });
                stream.OnProtocol += null;
                LastCall.IgnoreArguments();
            }

            using (mocks.Playback())
            {
                Room testRoom = cm.GetRoom(jid);
                testRoom.PrivateMessage(TO_NICK, MESSAGE);
            }
        }

        private const string REASON = "TestReason";

        [Test]
        public void RoomLeaveTest()
        {
            using (mocks.Record())
            {
                Expect.Call(stream.Document).Return(doc);
                stream.Write((XmlElement)null);
                LastCall.Callback((Func<XmlElement, bool>)
                    delegate(XmlElement elem)
                    {
                        string original = elem.OuterXml;
                        return original.Replace(" ", "") ==
                            GetLeavePresence().Replace(" ", "");
                    });
                stream.OnProtocol += null;
                LastCall.IgnoreArguments();
            }

            using (mocks.Playback())
            {
                Room testRoom = cm.GetRoom(jid);
                testRoom.Leave(REASON);
            }
        }

        private string GetLeavePresence()
        {
            return
                string.Format(
                    "<presence to=\"{0}\" type=\"unavailable\">" +
                        "<status>{1}</status>" +
                    "</presence>",
                    jid, REASON);
        }

        private string GetJoinPresence()
        {
            return
                string.Format(
                    "<presence to=\"{0}\">" +
                        "<x xmlns=\"{1}\"/>" +
                    "</presence>",
                    jid, URI.MUC);
        }

        private string GetRoomMessage(string id)
        {
            return
                string.Format(
                    "<message id=\"{0}\" to=\"{1}\" type=\"groupchat\">" +
                        "<body>{2}</body>" +
                    "</message>",
                    id, jid.Bare, MESSAGE);
        }

        private string GetRoomPrivateMessage(string id)
        {
            return
                string.Format(
                    "<message id=\"{0}\" to=\"{1}/{2}\" type=\"chat\">" +
                        "<body>{3}</body>" +
                    "</message>",
                    id, jid.Bare, TO_NICK, MESSAGE);
        }
    }
}