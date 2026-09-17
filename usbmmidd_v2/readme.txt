usbmmidd v2 - Amyuni USB Mobile Monitor Virtual Display driver
==============================================================

What this is, and why it is here
--------------------------------

usbmmidd_v2.zip is a virtual monitor driver. The Build workflow installs it so that the
test suite runs against a machine with two displays.

A build agent has a single display, and with one monitor the tests never reach the part of
this library that exists for several: relative positioning, and an arrangement key that
changes when monitors come and go. Attaching a virtual monitor gives
MonitorLayoutTests.A_real_multi_monitor_arrangement_is_coherent something real to check.

The driver is signed by Amyuni through Microsoft's driver signing programme and installs
without test signing or a reboot, which is what makes it usable on a hosted runner.

It is committed here rather than downloaded during the build. Amyuni publishes a single
unversioned URL and no releases, so a build that fetched it would silently change whenever
they replaced the file. A copy in the repository is the version, and it changes only when
someone commits a new one.

    File:    usbmmidd_v2.zip
    Source:  https://www.amyuni.com/downloads/usbmmidd_v2.zip
    SHA-256: 629b51e9944762bae73948171c65d09a79595cf4c771a82ebc003fbba5b24f51
    Size:    199,309 bytes
    Build:   binaries dated 2021-09-09

Updating it
-----------

1. Download the current file and look at what changed:

       curl -sSL -o usbmmidd_v2.zip https://www.amyuni.com/downloads/usbmmidd_v2.zip
       sha256sum usbmmidd_v2.zip

   If the digest matches the one above, there is nothing to do.

2. If it differs, review the new archive before committing it. It is a kernel-mode driver
   that CI installs on a machine holding a checkout of this repository, so treat a change
   of contents as something to read, not something to take on trust. Check that License.txt
   still permits redistribution and that the binaries are still signed by Amyuni:

       unzip -l usbmmidd_v2.zip
       unzip -p usbmmidd_v2.zip usbmmidd_v2/License.txt

3. Replace the zip, and update the digest, size and build date recorded above.

4. Push the change on a branch. The two-monitor legs of the Build workflow install the
   driver and run the suite against it, so a broken or incompatible version fails there
   rather than on someone's machine.

The workflow simply unpacks this archive and runs, from the usbmmidd_v2 directory inside it:

    deviceinstaller64.exe install usbmmidd.inf usbmmidd
    deviceinstaller64.exe enableidd 1

enableidd can be run up to four times to attach further monitors, and `enableidd 0` detaches
one. idd_instructions.txt inside the archive has Amyuni's own notes, including the registry
key that controls the offered resolutions.

Licence
-------

Reproduced from License.txt inside the archive, which ships with it unaltered. Note the
acknowledgement required by clause 1 - this file, and the entry in the workflow, are it.

    Copyright 2014-2021 Amyuni Technologies Inc.
    https://www.amyuni.com

    This software is provided 'as-is', without any express or implied warranty. In no event
    will the authors be held liable for any damages arising from the use of this software.
    By using this software, you accept to be prompted with an advertisement page which is
    not always under the control of the author. A fully commercial version is available for
    users who do not wisth to see those advertisements.

    Permission is granted to anyone to use this software for any purpose subject to the
    following restrictions:

    1. The origin of this software must not be misrepresented; you must not claim that you
       wrote the original software.
       If you use this software in a product, an acknowledgment in the product documentation
       would be required.
    2. Altered versions must be plainly marked as such, and must not be misrepresented as
       being the original software.
    3. This notice may not be removed or altered from any distribution.

The archive is committed byte for byte as Amyuni publishes it: nothing is altered, so
clause 2 does not apply. This driver is a test dependency only. It is not redistributed in
the RestoreWindowPosition package and no part of it is linked into the library.
