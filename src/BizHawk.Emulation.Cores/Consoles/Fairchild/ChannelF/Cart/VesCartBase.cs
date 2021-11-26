﻿using BizHawk.Common;
using BizHawk.Common.NumberExtensions;
using BizHawk.Emulation.Common;
using System.Collections;

namespace BizHawk.Emulation.Cores.Consoles.ChannelF
{
	public abstract class VesCartBase
	{
		public abstract string BoardType { get; }	

		public virtual void SyncByteArrayDomain(ChannelF sys)
		{
			sys.SyncByteArrayDomain("ROM", _rom);
		}

		public virtual byte[] ROM
		{
			get { return _rom; }
			protected set { _rom = value; }
		}
		protected byte[] _rom;		

		public virtual byte[] RAM
		{
			get { return _ram; }
			protected set { _ram = value; }
		}
		protected byte[] _ram;

		// SRAM config
		// taken from https://github.com/mamedev/mame/blob/ee1e4f9683a4953cb9d88f9256017fcbc38e3144/src/devices/bus/chanf/rom.cpp
		// (license:BSD-3-Clause - copyright-holders:Fabio Priuli)
		protected byte[] m_latch = new byte[2];
		protected ushort m_addr_latch;
		protected int m_addr;
		protected int m_read_write;
		protected int m_data0;

		public abstract byte ReadBus(ushort addr);
		public abstract void WriteBus(ushort addr, byte value);
		public abstract byte ReadPort(ushort addr);
		public abstract void WritePort(ushort addr, byte data);

		public static VesCartBase Configure(GameInfo gi, byte[] rom)
		{
			// get board type
			string boardStr = gi.OptionPresent("board") ? gi.GetStringValue("board") : "STD";

			switch (boardStr)
			{
				// standard cart layout - default to this
				case "STD":
				default:
					// any number of F3851 Program Storage Units (1KB ROM each) or F3856 Program Storage Unit (2KB ROM)
					// no on-pcb RAM and no extra IO
					return new mapper_STD(rom);

				case "MAZE":
					return new mapper_MAZE(rom);

				case "SCHACH":
				
					// F3853 Memory Interface Chip, 6KB of ROM and 2KB of RAM
					return new mapper_SCHACH(rom);

				case "HANG":

					return new mapper_HANG(rom);
			}
		}

		/// <summary>
		/// Read method for carts that have an IO-accessible 2102 SRAM chip
		/// Based on: https://github.com/mamedev/mame/blob/ee1e4f9683a4953cb9d88f9256017fcbc38e3144/src/devices/bus/chanf/rom.cpp
		/// license:BSD-3-Clause
		/// copyright-holders:Fabio Priuli
		/// </summary>
		public byte SRAM2102_Read(int index)
		{
			if (index == 0)
			{
				if (m_read_write == 0)
				{
					m_addr = m_addr_latch;
					m_data0 = RAM[m_addr] & 1;
					return (byte)((m_latch[0] & 0x7f) | (m_data0 << 7));
				}

				return m_latch[0];
			}
			else
			{
				return m_latch[1];
			}
		}

		/// <summary>
		/// Write method for carts that have an IO-accessible 2102 SRAM chip
		/// Based on: https://github.com/mamedev/mame/blob/ee1e4f9683a4953cb9d88f9256017fcbc38e3144/src/devices/bus/chanf/rom.cpp
		/// license:BSD-3-Clause
		/// copyright-holders:Fabio Priuli
		/// </summary>
		public void SRAM2102_Write(int index, byte data)
		{
			if (index == 0)
			{
				m_latch[0] = data;

				m_read_write = data.Bit(0) ? 1 : 0;// BIT(data, 0);

				//m_addr_latch = (m_addr_latch & 0x3f3) | (BIT(data, 2) a<< 2) | (BIT(data, 1) << 3);  // bits 2,3 come from this write!
				m_addr_latch = (ushort)((m_addr_latch & 0x3f3) | ((data.Bit(2) ? 1 : 0) << 2) | ((data.Bit(1) ? 1 : 0) << 3));  // bits 2,3 come from this write!

				m_addr = m_addr_latch;

				m_data0 = data.Bit(3) ? 1 : 0; // BIT(data, 3);

				if (m_read_write == 1)
				{
					RAM[m_addr] = (byte)m_data0;
				}					
			}
			else
			{
				m_latch[1] = data;
				// all bits but 2,3 come from this write, but they are shuffled
				// notice that data is 8bits, so when swapping bit8 & bit9 are always 0!
				//m_addr_latch = (m_addr_latch & 0x0c) | (bitswap < 16 > ((uint16_t)data, 15, 14, 13, 12, 11, 10, 7, 6, 5, 3, 2, 1, 9, 8, 4, 0));

				BitArray b = new BitArray(16);
				b[3] = false;// data.Bit(3);
				b[2] = false; // data.Bit(2);

				b[9] = data.Bit(7);
				b[8] = data.Bit(6);
				b[7] = data.Bit(5);

				b[6] = data.Bit(3);
				b[5] = data.Bit(2);
				b[4] = data.Bit(1);

				b[1] = data.Bit(4);

				b[0] = data.Bit(0);

				var resBytes = new byte[4];
				b.CopyTo(resBytes, 0);
				m_addr_latch = (ushort)(resBytes[0] | resBytes[1] << 8);
			}
		}		

		public void Reset()
		{
			m_latch[0] = 0;
			m_latch[1] = 0;
			m_addr = 0;
			m_addr_latch = 0;
			m_read_write = 0;
			m_data0 = 0;
		}

		public virtual void SyncState(Serializer ser)
		{
			ser.BeginSection("Cart");
			ser.Sync(nameof(RAM), ref _ram, false);
			ser.Sync(nameof(m_latch), ref m_latch, false);
			ser.Sync(nameof(m_addr_latch), ref m_addr_latch);
			ser.Sync(nameof(m_addr), ref m_addr);
			ser.Sync(nameof(m_read_write), ref m_read_write);
			ser.Sync(nameof(m_data0), ref m_data0);
			ser.EndSection();
		}
	}
}
