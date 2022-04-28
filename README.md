# spine-optimize
Spint for Unity Version 2.1
First：
Convert JSON to binary.

Second:
Use pointer movement instead of Stream read.

Once again, to optimize:
All strings moved to the header, using index instead.
Delete nonessential data.
Merge Timeline.
So the binary 50%-70% smaller.
