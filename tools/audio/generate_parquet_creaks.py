#!/usr/bin/env python3
"""Quiet synthesized wood flex variants; local preview foley, no recording claim."""
from pathlib import Path
import wave
import numpy as np
from scipy import signal
root=Path(__file__).resolve().parents[2]/'game/assets/audio/act1/foley/parquet'
root.mkdir(parents=True,exist_ok=True)
rng=np.random.default_rng(20260930)
rate=44100
for i in range(3):
 t=np.arange(int(rate*(.42+i*.07)))/rate
 # Short stick-slip friction and a decaying timber resonance, no door hinge.
 envelope=np.sin(np.pi*t/t[-1])**2*np.exp(-t*3)
 frequency=260+i*37+95*np.sin(t*6)+28*np.sin(t*37)
 phase=2*np.pi*np.cumsum(frequency)/rate
 friction=signal.sosfilt(signal.butter(2,[170,1700],btype='bandpass',fs=rate,output='sos'),rng.normal(0,1,len(t)))
 flex=(np.sin(phase)*.42+np.sin(phase*2.03)*.12+friction*.12)*envelope
 flex+=np.sin(2*np.pi*(120+i*12)*t)*np.exp(-t*24)*.06
 flex=flex/max(np.max(np.abs(flex)),1e-6)*.24
 with wave.open(str(root/f'creak_{i:02}.wav'),'wb') as w:
  w.setparams((1,2,rate,0,'NONE','not compressed'));w.writeframes((flex*32767).astype('<i2').tobytes())
 print(root/f'creak_{i:02}.wav')
