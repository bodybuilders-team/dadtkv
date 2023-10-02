# ist-meic-dad-g05

> Design and Implementation of Distributed Applications project of group 05 - MEIC @ IST 2022/2023.

## Authors

- [110817 André Páscoa](https://github.com/devandrepascoa)
- [110860 André Jesus](https://github.com/andre-j3sus)
- [110893 Nyckollas Brandão](https://github.com/Nyckoka)

Professors: Paolo Romano and João Garcia

@IST<br>
Master in Computer Science and Computer Engineering<br>
Design and Implementation of Distributed Applications - Group 05<br>
Winter Semester of 2023/2024

---

## Organization Description

The solution to the project is divided into 4 projects:

* **DADTKVClient**: Client application that communicates with the Transaction Managers, using the DADTKVService
  interface.
* **DADTKVTransactionManager**: Transaction Manager application that communicates with the Lease Managers, using the
  LeaseService interface.
* **DADTKVLeaseManager**: Lease Manager application.7
* **DADTKVCore**: Contains the interfaces and classes that are common to all the other projects, including the
  configuration of the system.

...